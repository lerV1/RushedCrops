using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RushedCrops.Editor
{
    // Keeps the Holotna layout/prefab links while giving the map solid, faceted silhouettes.
    public static class CartoonWorldStyler
    {
        private const string MaterialFolder = "Assets/_Game/Materials/Cartoon";
        private const string MeshFolder = "Assets/_Game/Models/Cartoon";
        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
        private static readonly Dictionary<string, Material> Palette = new Dictionary<string, Material>();

        [MenuItem("Rushed Crops/World/Apply low poly cartoon style")]
        public static void Apply()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Style the saved map outside Play.");
            var root = GameObject.Find(FarmWorldBuilder.RootName);
            if (root == null) throw new InvalidOperationException("Build the farm map first.");
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/_Game/Materials", "Cartoon");
            if (!AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.CreateFolder("Assets/_Game/Models", "Cartoon");
            Meshes.Clear(); Palette.Clear();
            CreatePalette();
            CreateMeshes();

            // Snapshot the imported prefab roots before adding any visual children.
            var imports = new List<GameObject>();
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                if (PrefabUtility.IsOutermostPrefabInstanceRoot(transform.gameObject)) imports.Add(transform.gameObject);
            foreach (GameObject imported in imports) StyleImported(imported);
            StyleGround();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("Cartoon style applied: faceted trees, solid plants, flat shaded props, bright terrain.");
        }

        private static void CreatePalette()
        {
            Colour("Bark", 0.38f, 0.22f, 0.10f);
            Colour("Wood", 0.58f, 0.35f, 0.17f);
            Colour("LightWood", 0.72f, 0.49f, 0.25f);
            Colour("DarkWood", 0.39f, 0.24f, 0.12f);
            Colour("Roof", 0.68f, 0.35f, 0.16f);
            Colour("Wall", 0.76f, 0.57f, 0.32f);
            Colour("Stone", 0.51f, 0.55f, 0.54f);
            Colour("StoneLight", 0.65f, 0.66f, 0.58f);
            Colour("Iron", 0.27f, 0.32f, 0.32f);
            Colour("Soil", 0.38f, 0.23f, 0.11f);
            Colour("Straw", 0.87f, 0.65f, 0.24f);
            Colour("Leaf0", 0.38f, 0.59f, 0.16f);
            Colour("Leaf1", 0.55f, 0.66f, 0.20f);
            Colour("Leaf2", 0.26f, 0.47f, 0.17f);
            Colour("Leaf3", 0.67f, 0.71f, 0.28f);
            Colour("Pine0", 0.25f, 0.46f, 0.22f);
            Colour("Pine1", 0.33f, 0.54f, 0.23f);
            Colour("FlowerCream", 1f, 0.88f, 0.53f);
            Colour("FlowerCoral", 0.91f, 0.42f, 0.25f);
            Colour("FlowerLilac", 0.65f, 0.48f, 0.77f);
            Colour("Carrot", 0.96f, 0.44f, 0.10f);
            Colour("Beet", 0.66f, 0.24f, 0.34f);
            Colour("Glass", 1f, 0.69f, 0.26f);
            Colour("Canvas", 0.92f, 0.80f, 0.57f);
        }

        private static void CreateMeshes()
        {
            Meshes["Crown"] = SaveMesh(Icosphere(1), "Crown");
            Meshes["Boulder"] = SaveMesh(Icosphere(0), "Boulder");
            Meshes["Cone"] = SaveMesh(TaperedCylinder(7, 1f, 0f), "Cone");
            Meshes["Trunk"] = SaveMesh(TaperedCylinder(6, 1f, 0.7f), "Trunk");
            var vertices = new List<Vector3>();
            for (int i = 0; i < 5; i++)
            {
                Quaternion turn = Quaternion.Euler(0f, i * 137.5f, 0f);
                Vector3 a = turn * new Vector3(-0.1f, 0f, 0f);
                Vector3 b = turn * new Vector3(0.1f, 0f, 0f);
                Vector3 c = turn * new Vector3(0.12f, 0.5f + i * 0.07f, 0.18f);
                vertices.AddRange(new[] { a, b, c, c, b, a });
            }
            Meshes["Grass"] = SaveMesh(FromTriangles(vertices), "Grass");
        }

        private static void StyleImported(GameObject imported)
        {
            // The game's own copies of pack prefabs carry a PFB_ prefix; style them like the originals.
            string name = imported.name.StartsWith("PFB_") ? imported.name.Substring(4) : imported.name;
            bool tree = name.StartsWith("Oak0") || name.StartsWith("Pine0") || name.StartsWith("Spruce0") || name.StartsWith("Willow0");
            bool shrub = name.StartsWith("Bush") || name.StartsWith("Fern");
            bool grass = name.StartsWith("Grass") || name.StartsWith("Cattail");
            bool flower = name.StartsWith("Flower");
            bool carrot = name.StartsWith("CarrotPlant") || name.StartsWith("BeetPlant") || name.StartsWith("CabbagePlant") || name.StartsWith("OnionPlant");
            bool wheat = name.StartsWith("WheatPlant");

            Transform previous = imported.transform.Find("Cartoon visuals");
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            foreach (Renderer renderer in imported.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.GetComponent<TextMesh>() != null) continue;
                if (tree || shrub || grass || flower || carrot || wheat) renderer.enabled = false;
                else
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                        if (materials[i] != null) materials[i] = ForMaterial(materials[i].name);
                    renderer.sharedMaterials = materials;
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null && filter.sharedMesh != null && !AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(MeshFolder + "/"))
                    {
                        string key = AssetDatabase.GetAssetPath(filter.sharedMesh) + "/" + filter.sharedMesh.name;
                        if (!Meshes.TryGetValue(key, out Mesh flat))
                        {
                            flat = FlatCopy(filter.sharedMesh);
                            // A stable GUID plus mesh local ID avoids collisions between modular pack meshes.
                            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(filter.sharedMesh, out string guid, out long id);
                            flat = SaveMesh(flat, "Flat_" + guid + "_" + id);
                            Meshes[key] = flat;
                        }
                        filter.sharedMesh = flat;
                    }
                }
            }
            if (!(tree || shrub || grass || flower || carrot || wheat))
            {
                // Holotna's planted patches contain nested crop prefabs as well as soil ridges.
                foreach (LODGroup lod in imported.GetComponentsInChildren<LODGroup>(true))
                    if (lod.gameObject.activeInHierarchy && lod.name.Contains("Plant")) StyleImported(lod.gameObject);
                return;
            }
            foreach (LODGroup lod in imported.GetComponentsInChildren<LODGroup>(true)) lod.enabled = false;
            foreach (Collider collider in imported.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            var visuals = new GameObject("Cartoon visuals").transform;
            visuals.SetParent(imported.transform, false);
            int variant = Mathf.Abs(Mathf.RoundToInt(imported.transform.position.x * 13f + imported.transform.position.z * 7f)) % 4;

            if (tree) BuildTree(imported, visuals, variant);
            else if (shrub)
            {
                Piece("Rounded bush", visuals, "Crown", new Vector3(0,0.45f,0), new Vector3(1.1f,0.8f,1f), "Leaf"+variant);
                Piece("Bush side", visuals, "Boulder", new Vector3(0.45f,0.3f,0.2f), new Vector3(0.7f,0.65f,0.7f), "Leaf"+((variant+1)%4));
            }
            else if (grass) Piece("Solid grass blades", visuals, "Grass", Vector3.zero, Vector3.one, "Leaf0");
            else if (flower)
            {
                string[] flowers = { "FlowerCream", "FlowerCoral", "FlowerLilac" };
                for (int i = 0; i < 3; i++)
                {
                    var p = new Vector3((i-1)*0.32f,0, i%2*0.28f);
                    Piece("Flower stem",visuals,"Trunk",p+Vector3.up*0.25f,new Vector3(0.025f,0.5f,0.025f),"Leaf2");
                    Piece("Flower blossom",visuals,"Boulder",p+Vector3.up*0.53f,new Vector3(0.26f,0.16f,0.26f),flowers[(i+variant)%3]);
                }
            }
            else if (carrot)
            {
                string bulb=name.StartsWith("Beet")?"Beet":name.StartsWith("Cabbage")?"Leaf1":name.StartsWith("Onion")?"Canvas":"Carrot";
                Piece("Crop shoulder",visuals,"Boulder",new Vector3(0,0.12f,0),new Vector3(0.25f,0.3f,0.25f),bulb);
                Piece("Carrot leaves",visuals,"Grass",new Vector3(0,0.2f,0),Vector3.one*0.8f,"Leaf0");
            }
            else if (wheat)
            {
                for(int i=0;i<3;i++)
                {
                    Vector3 p=new Vector3((i-1)*0.18f,0,0);
                    Piece("Wheat stem",visuals,"Trunk",p+Vector3.up*0.5f,new Vector3(0.03f,1f,0.03f),"Straw");
                    Piece("Wheat head",visuals,"Crown",p+Vector3.up*1.05f,new Vector3(0.15f,0.4f,0.15f),"Straw");
                }
            }
        }

        private static void BuildTree(GameObject imported, Transform parent, int variant)
        {
            bool round = imported.name.StartsWith("Oak") || imported.name.StartsWith("Willow");
            float height = imported.name.EndsWith("02") ? 5.8f : 4.4f;
            Piece("Faceted trunk",parent,"Trunk",new Vector3(0,height*0.3f,0),new Vector3(0.24f,height*0.6f,0.24f),"Bark");
            var collider = parent.gameObject.AddComponent<CapsuleCollider>();
            collider.radius=0.26f;collider.height=height*0.6f;collider.center=new Vector3(0,height*0.3f,0);
            parent.gameObject.layer=6;
            if (round)
            {
                Piece("Main faceted crown",parent,"Crown",new Vector3(0,height*0.72f,0),new Vector3(height*0.55f,height*0.49f,height*0.52f),"Leaf"+variant);
                Piece("Low crown",parent,"Boulder",new Vector3(-height*0.24f,height*0.61f,0.2f),new Vector3(height*0.36f,height*0.32f,height*0.35f),"Leaf"+((variant+1)%4));
                Piece("Side crown",parent,"Crown",new Vector3(height*0.24f,height*0.75f,-0.3f),new Vector3(height*0.34f,height*0.33f,height*0.34f),"Leaf"+variant);
            }
            else
            {
                for (int i=0;i<4;i++)
                {
                    float radius=height*(0.37f-i*0.073f);
                    Piece("Layered pine crown",parent,"Cone",new Vector3(0,height*(0.39f+i*0.17f),0),new Vector3(radius,height*0.47f,radius),"Pine"+(i%2));
                }
            }
        }

        private static Material ForMaterial(string source)
        {
            if(Palette.TryGetValue(source,out Material existing))return existing;
            string n=source.ToLowerInvariant();
            if(n.Contains("roof"))return Palette["Roof"];
            if(n.Contains("wall"))return Palette["Wall"];
            if(n.Contains("quarry")||n.Contains("block")||n.Contains("rubble"))return Palette["StoneLight"];
            if(n.Contains("rock")||n.Contains("stone")||n.Contains("pebble"))return Palette["Stone"];
            if(n.Contains("soil")||n.Contains("ridge"))return Palette["Soil"];
            if(n.Contains("straw")||n.Contains("wheat"))return Palette["Straw"];
            if(n.Contains("lantern"))return Palette["Iron"];
            if(n.Contains("tent")||n.Contains("bedroll")||n.Contains("rope"))return Palette["Canvas"];
            if(n.Contains("tools")||n.Contains("hook"))return Palette["Iron"];
            if(n.Contains("carrot")||n.Contains("beet"))return Palette["Carrot"];
            if(n.Contains("grass")||n.Contains("lily")||n.Contains("fern")||n.Contains("bush"))return Palette["Leaf0"];
            if(n.Contains("wood02")||n.Contains("crate"))return Palette["LightWood"];
            if(n.Contains("wood04")||n.Contains("stump"))return Palette["DarkWood"];
            return Palette["Wood"];
        }

        private static void StyleGround()
        {
            Ground("Meadow",new Color(0.44f,0.61f,0.25f));
            Ground("MeadowLight",new Color(0.51f,0.66f,0.28f));
            Ground("MeadowShade",new Color(0.39f,0.55f,0.22f));
            Ground("MeadowGold",new Color(0.59f,0.67f,0.30f));
            Ground("Path",new Color(0.72f,0.54f,0.31f));
            Ground("PathShoulder",new Color(0.61f,0.49f,0.27f));
            Ground("Gravel",new Color(0.66f,0.62f,0.48f));
            Ground("Water",new Color(0.13f,0.55f,0.67f));
            Ground("WaterLight",new Color(0.20f,0.62f,0.70f));
        }
        private static void Ground(string name,Color color)
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/World/"+name+".mat");
            material.SetTexture("_BaseMap",null);material.SetTexture("_BumpMap",null);material.color=color;EditorUtility.SetDirty(material);
        }
        private static void Colour(string name,float r,float g,float b)
        {
            string path=MaterialFolder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            material.color=new Color(r,g,b);material.SetFloat("_Smoothness",0);material.enableInstancing=true;Palette[name]=material;EditorUtility.SetDirty(material);
        }
        private static void Piece(string name,Transform parent,string mesh,Vector3 position,Vector3 size,string material)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;go.GetComponent<MeshFilter>().sharedMesh=Meshes[mesh];go.GetComponent<Renderer>().sharedMaterial=Palette[material];
        }
        private static Mesh SaveMesh(Mesh mesh,string name)
        {
            mesh.name=name;string path=MeshFolder+"/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}
            return mesh;
        }
        private static Mesh FlatCopy(Mesh source)
        {
            var vertices=new List<Vector3>();var submeshes=new List<int[]>();
            for(int s=0;s<source.subMeshCount;s++)
            {
                var indices=source.GetTriangles(s);var flat=new int[indices.Length];var positions=source.vertices;
                for(int i=0;i<indices.Length;i++){flat[i]=vertices.Count;vertices.Add(positions[indices[i]]);}submeshes.Add(flat);
            }
            var mesh=new Mesh{indexFormat=vertices.Count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16};mesh.SetVertices(vertices);mesh.subMeshCount=submeshes.Count;
            for(int s=0;s<submeshes.Count;s++)mesh.SetTriangles(submeshes[s],s);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private static Mesh FromTriangles(List<Vector3> vertices)
        {
            var mesh=new Mesh();mesh.SetVertices(vertices);int[] indices=new int[vertices.Count];for(int i=0;i<indices.Length;i++)indices[i]=i;mesh.triangles=indices;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private static Mesh TaperedCylinder(int sides,float bottom,float top)
        {
            var vertices=new List<Vector3>();
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                Vector3 p=new Vector3(Mathf.Cos(a)*bottom,-0.5f,Mathf.Sin(a)*bottom),q=new Vector3(Mathf.Cos(b)*bottom,-0.5f,Mathf.Sin(b)*bottom);
                Vector3 r=new Vector3(Mathf.Cos(a)*top,0.5f,Mathf.Sin(a)*top),s=new Vector3(Mathf.Cos(b)*top,0.5f,Mathf.Sin(b)*top);
                vertices.AddRange(new[]{p,r,q,q,r,s,new Vector3(0,-0.5f,0),p,q,new Vector3(0,0.5f,0),s,r});
            }
            return FromTriangles(vertices);
        }
        private static Mesh Icosphere(int subdivisions)
        {
            float t=(1f+Mathf.Sqrt(5f))/2f;
            var p=new[]{new Vector3(-1,t,0),new Vector3(1,t,0),new Vector3(-1,-t,0),new Vector3(1,-t,0),new Vector3(0,-1,t),new Vector3(0,1,t),new Vector3(0,-1,-t),new Vector3(0,1,-t),new Vector3(t,0,-1),new Vector3(t,0,1),new Vector3(-t,0,-1),new Vector3(-t,0,1)};
            int[] faces={0,11,5,0,5,1,0,1,7,0,7,10,0,10,11,1,5,9,5,11,4,11,10,2,10,7,6,7,1,8,3,9,4,3,4,2,3,2,6,3,6,8,3,8,9,4,9,5,2,4,11,6,2,10,8,6,7,9,8,1};
            var triangles=new List<Vector3>();foreach(int i in faces)triangles.Add(p[i].normalized*0.5f);
            for(int sub=0;sub<subdivisions;sub++)
            {
                var next=new List<Vector3>();
                for(int i=0;i<triangles.Count;i+=3)
                {
                    Vector3 a=triangles[i],b=triangles[i+1],c=triangles[i+2],ab=(a+b).normalized*0.5f,bc=(b+c).normalized*0.5f,ca=(c+a).normalized*0.5f;
                    next.AddRange(new[]{a,ab,ca,b,bc,ab,c,ca,bc,ab,bc,ca});
                }
                triangles=next;
            }
            return FromTriangles(triangles);
        }
    }
}
