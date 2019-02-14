using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Runtime.InteropServices;

public class VertexTool : EditorWindow
{
    // Add menu named "My Window" to the Window menu
    [MenuItem("Tools/Vertex Colours")]
    static void Init()
    {
        // Get existing open window or if none, make a new one:
        VertexTool window = (VertexTool)EditorWindow.GetWindow(typeof(VertexTool));
        window.Show();
    }

    private enum Flags : uint
    {
        Selected = 1,
        Backfacing = 2
    }

    // Rendering resources
    private Material pointMaterial;
    private ComputeBuffer gpu_vertices;
    private ComputeBuffer gpu_normals;
    private ComputeBuffer gpu_colours;
    private ComputeBuffer gpu_flags;
    private uint[] cpu_flags;

    private List<int> selected;

    // Visualisation properties
    private bool cullBackfacing = true;
    private float size = 0.2f;

    // Tool settings
    private float brushSize = 10f;

    // State properties
    private bool painting = false;

    void OnEnable()
    {
        SceneView.onSceneGUIDelegate += this.OnSceneGUI;

        if (pointMaterial == null)
        {
            pointMaterial = new Material(Shader.Find("VertexTools/PointShader"));
        }
        if (gpu_vertices == null)
        {
            gpu_vertices = new ComputeBuffer((int)ushort.MaxValue, Marshal.SizeOf(typeof(Vector3)));
        }
        if (gpu_normals == null)
        {
            gpu_normals = new ComputeBuffer((int)ushort.MaxValue, Marshal.SizeOf(typeof(Vector3)));
        }
        if (gpu_colours == null)
        {
            gpu_colours = new ComputeBuffer((int)ushort.MaxValue, Marshal.SizeOf(typeof(Color)));
        }
        if (gpu_flags == null)
        {
            gpu_flags = new ComputeBuffer((int)ushort.MaxValue, Marshal.SizeOf(typeof(uint)));
            cpu_flags = new uint[gpu_flags.count];
        }

        if(selected == null)
        {
            selected = new List<int>();
        }
    }

    void OnDisable()
    {
        SceneView.onSceneGUIDelegate -= this.OnSceneGUI;

        gpu_vertices.Dispose();
        gpu_vertices = null;

        gpu_normals.Dispose();
        gpu_normals = null;

        gpu_colours.Dispose();
        gpu_colours = null;

        gpu_flags.Dispose();
        gpu_flags = null;

        painting = false;
    }

    void OnGUI()
    {
        GUILayout.Label("Settings", EditorStyles.boldLabel);
        size = EditorGUILayout.Slider("Point Size", size, 0.01f, 10f);
        cullBackfacing = EditorGUILayout.Toggle("Cull Backfacing", cullBackfacing);

        brushSize = EditorGUILayout.Slider("Brush Size", brushSize, 0.1f, 100f);

        EditorGUILayout.LabelField("Selected: " + selected.Count);
        EditorGUILayout.LabelField("Painting: " + painting);
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if(Selection.activeGameObject == null)
        {
            return;
        }

        var gameobject = Selection.activeGameObject;

        Mesh mesh = null;

        var renderer = gameobject.GetComponent<MeshFilter>();
        if (renderer != null)
        {
            mesh = renderer.sharedMesh;
        }

        var skinnedrenderer = gameobject.GetComponent<SkinnedMeshRenderer>();
        if( skinnedrenderer != null)
        {
            mesh = skinnedrenderer.sharedMesh;
        }

        if(mesh == null)
        {
            return;
        }

        var transform = gameobject.transform.localToWorldMatrix;

        gpu_vertices.SetData(mesh.vertices);
        gpu_normals.SetData(mesh.normals);
        gpu_colours.SetData(mesh.colors);

        pointMaterial.SetBuffer("points", gpu_vertices);
        pointMaterial.SetBuffer("normals", gpu_normals);
        pointMaterial.SetBuffer("colours", gpu_colours);
        //pointMaterial.SetBuffer("flags", gpu_flags); //must use SetRandomWriteTarget instead.
        Graphics.SetRandomWriteTarget(1, gpu_flags, true);

        pointMaterial.SetMatrix("ObjectToWorld", transform);

        pointMaterial.SetFloat("Size", size);

        var mousePosition = Event.current.mousePosition;
        mousePosition.y = sceneView.camera.pixelHeight - mousePosition.y;
        var tool = new Vector4(mousePosition.x, mousePosition.y, brushSize, painting ? 1 : -1);
        pointMaterial.SetVector("Tool", tool);

        pointMaterial.SetInt("CullBackfacing", cullBackfacing ? 1 : 0);

        pointMaterial.SetPass(0);
        Graphics.DrawProcedural(MeshTopology.Points, mesh.vertexCount);
        
        Handles.BeginGUI();

        if (mouseOverWindow is SceneView)
        {
            if (Event.current.type == EventType.Repaint)
            {
                DrawCircleBrush(Color.white, brushSize);
            }
        }

        Handles.EndGUI();

        int controlid = GUIUtility.GetControlID(FocusType.Passive);

        if (Event.current.button == 0)
        {
            switch (Event.current.type)
            {
                case EventType.MouseDown:
                case EventType.MouseDrag:

                    gpu_flags.GetData(cpu_flags);
                    for (int i = 0; i < mesh.vertexCount; i++)
                    {
                        if (((Flags)cpu_flags[i] & Flags.Selected) > 0)
                        {
                            selected.Add(i);
                        }
                    }

                    GUIUtility.hotControl = controlid;
                    Event.current.Use();

                    break;
            }
        }

        sceneView.Repaint();
    }

    //https://forum.unity.com/threads/how-can-i-draw-a-circle-around-the-mouse-pointer-on-scene-view.474273/
    private static void DrawCircleBrush(Color color, float size)
    {
        Handles.color = color;
        // Circle
        Handles.CircleHandleCap(0, Event.current.mousePosition, Quaternion.identity, size, EventType.Repaint);
        // Cross Center
        Handles.DrawLine(Event.current.mousePosition + Vector2.left, Event.current.mousePosition + Vector2.right);
        Handles.DrawLine(Event.current.mousePosition + Vector2.up, Event.current.mousePosition + Vector2.down);
    }
}
