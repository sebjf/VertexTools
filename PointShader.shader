Shader "VertexTools/PointShader"
{
	Properties
	{
	}
	SubShader
	{
		Tags{ "RenderType" = "Opaque" }
		LOD 100

		ZTest Always
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma geometry geom
			#pragma target 5.0
		
			#include "UnityCG.cginc"

			StructuredBuffer<float3> points;
			StructuredBuffer<float3> normals;
			StructuredBuffer<float4> colours;

			RWStructuredBuffer<uint> flags : register(u1);

			float4x4 ObjectToWorld;	

			float Size;
			float4 Tool;
			int CullBackfacing;

			struct v2g
			{
				float4 vertex : SV_POSITION;
				float4 colour : COLOR;
				int index : TEXCOORD1;
				uint flags : TEXCOORD2;
			};

			struct g2f
			{
				float4 vertex : SV_POSITION;
				float4 colour : COLOR;
				float4 screenpos : TEXCOORD0;
				int index : TEXCOORD1;
				uint flags : TEXCOORD2;
			};

			// because unity_ObjectToWorld is not set properly when using DrawProcedural yet we have to do these explicitly

			inline float4 ObjectToClipPos(in float3 pos)	
			{
				return mul(UNITY_MATRIX_VP, mul(ObjectToWorld, float4(pos, 1.0)));
			}

			inline float4 ObjectToWorldDir(in float3 dir)
			{
				return mul(ObjectToWorld, float4(dir, 0.0));
			}

			v2g vert(uint id : SV_VertexID)
			{
				v2g o;
				o.vertex = ObjectToClipPos(points[id]);
				o.colour = colours[id];
				o.index = id;
				o.flags = 0;

				o.colour.a = 1;

				// execute the selection/painting tool

				// how to use computescreenpos properly: https://forum.unity.com/threads/what-does-the-function-computescreenpos-in-unitycg-cginc-do.294470/

				float4 screenpos = ComputeScreenPos(o.vertex);
				float2 screen = (screenpos.xy / screenpos.w) * _ScreenParams.xy;

				if (length(screen - Tool.xy) < Tool.z)
				{
					if (Tool.w > 0) 
					{
						o.colour = float4(1, 0, 0, 1) - (o.colour * 0.9) + (o.colour * 0.9);
					}
					else
					{
						o.colour = float4(1, 1, 1, 1) - (o.colour * 0.9) + (o.colour * 0.9);
					}
				
					o.flags = o.flags | 1;
				}

				// check for backfacing vertices

				float3 worldNormal = ObjectToWorldDir(normals[id]);
				float3 forward = mul((float3x3)unity_CameraToWorld, float3(0, 0, 1));	// https://answers.unity.com/questions/192553/camera-forward-vector-in-shader.html

				if (dot(worldNormal, forward) > 0)
				{
					o.flags = o.flags | 2;
				}

				// modulate the colour based on the normal

				o.colour = o.colour * max(-dot(worldNormal, forward), 0.3);
				
				return o;
			}

			[maxvertexcount(4)]
			void geom(point v2g p[1], inout TriangleStream<g2f> triStream)
			{
				float3 right = float3(1, 0, 0);
				float3 up = float3(0, 1.5, 0);
				float halfS = 0.015f * Size;
				g2f v[4];

				// apply the perspective correction now so the box will be sized in clip space, and so consistent on screen no matter the depth
				p[0].vertex.xyz = p[0].vertex.xyz / p[0].vertex.w; 

				v[0].vertex = float4(p[0].vertex + halfS * right - halfS * up, 1);
				v[1].vertex = float4(p[0].vertex + halfS * right + halfS * up, 1);
				v[2].vertex = float4(p[0].vertex - halfS * right - halfS * up, 1);
				v[3].vertex = float4(p[0].vertex - halfS * right + halfS * up, 1);

				[unroll]
				for (int i = 0; i < 4; i++)
				{
					v[i].screenpos = ComputeScreenPos(v[i].vertex);
				}

				//	#pragma warning( disable : 3078 ) // unity doesnt like trying to turn off the warning

				[unroll]
				for (int i = 0; i < 4; i++)
				{
					v[i].colour = p[0].colour;
					v[i].index = p[0].index;
					v[i].flags = p[0].flags;
				}

				triStream.Append(v[0]);
				triStream.Append(v[1]);
				triStream.Append(v[2]);
				triStream.Append(v[3]);
			}

			fixed4 frag(g2f i) : SV_Target
			{
				flags[i.index] = i.flags;

				fixed4 col = i.colour;
				
				if (CullBackfacing) 
				{
					if (i.flags & 2)
					{
						col.a = 0;
					}
				}

				return col;
			}

			ENDCG
		}
	}
}
