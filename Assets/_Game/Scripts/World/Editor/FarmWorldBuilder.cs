using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RushedCrops.Editor
{
    // Editor-only authoring tool. The saved scene contains the map; nothing is spawned at runtime.
    public static class FarmWorldBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/SC_Farm.unity";
        public const string RootName = "WORLD_RushedCrops";
        private const string Pack = "Assets/Holotna/Medieval Rural/Prefabs/";
        private const string Generated = "Assets/_Game/World/";
        private const int GroundLayer = 6;
        private static readonly Vector2 Barn = new Vector2(-36f, -20f);
        private static readonly Vector2 Garden = new Vector2(-35f, -44f);
        private static readonly Vector2 Market = new Vector2(0f, 0f);
        private static readonly Vector2 Quarry = new Vector2(39f, 39f);
        private static readonly Vector2 Logging = new Vector2(39f, -35f);
        private static readonly Vector2[] RiverGuide = {
            V(-108, 40), V(-85, 42), V(-68, 51), V(-49, 56), V(-27, 64), V(-3, 69),
            V(23, 67), V(48, 70), V(65, 57), V(76, 35), V(73, 12), V(80, -13),
            V(75, -35), V(87, -60), V(108, -74)
        };
        private static readonly Vector2[][] RouteGuides = {
            new[] { V(-35,-36), V(-24,-30), V(-16,-17), V(-5,-8), V(0,0) },
            new[] { V(0,0), V(10,13), V(23,23), V(34,30), V(40,37) },
            new[] { V(2,-7), V(14,-19), V(26,-27), V(40,-35) },
            new[] { V(-31,-49), V(-12,-54), V(9,-50), V(26,-44), V(40,-35) },
            new[] { V(-43,-29), V(-57,-10), V(-61,12), V(-60,28), V(-59,39), V(-66,51), V(-75,65), V(-55,77), V(-26,78) },
            new[] { V(40,-35), V(53,-28), V(65,-28), V(78,-28), V(89,-28), V(94,-10), V(89,13), V(86,31) }
        };
        private static readonly Vector2[] River = Smooth(RiverGuide);
        private static readonly Vector2[][] Routes = SmoothRoutes(RouteGuides);
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static readonly List<Light> Lanterns = new List<Light>();
        private static System.Random random;
        private static Transform world;

        [MenuItem("Rushed Crops/World/Build concept map")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play before authoring the map.");
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open SC_Farm before building its map.");
            if (GameObject.Find(RootName) != null)
                throw new InvalidOperationException("The map already exists. Edit its saved objects, or remove its WORLD root explicitly before rebuilding.");

            random = new System.Random(451);
            Materials.Clear();
            Lanterns.Clear();
            EnsureFolder("Assets/_Game/World");
            EnsureFolder(Generated + "Meshes");
            EnsureFolder(Generated + "Materials");
            world = new GameObject(RootName).transform;
            Undo.RegisterCreatedObjectUndo(world.gameObject, "Build Rushed Crops world");

            MakeMaterials();
            BuildGround(Group("00_Terrain"));
            BuildPaths(Group("01_Paths"));
            BuildRiver(Group("02_RiverAndBridges"));
            BuildBarn(Group("03_BarnAndPlantingPlots"));
            BuildMarket(Group("04_MerchantClearing"));
            BuildQuarry(Group("05_StoneQuarry"));
            BuildLogging(Group("06_LoggingArea"));
            BuildForest(Group("07_ForestAndMeadows"));
            BuildSigns(Group("08_Wayfinding"));
            BuildBoundaries(Group("09_WorldBoundary"));
            CartoonWorldStyler.Apply();
            BuildEnvironment();
            WorldGrounding.Apply();

            // Keep the original test ground available, and keep all player/input components intact.
            var oldGround = GameObject.Find("Ground");
            if (oldGround != null) { Undo.RecordObject(oldGround, "Disable test ground"); oldGround.SetActive(false); }
            var player = GameObject.Find("Player");
            if (player != null)
            {
                Undo.RecordObject(player.transform, "Place farm arrival");
                player.transform.position = new Vector3(-20f, Height(-20f, -38f) + 1.1f, -38f);
                player.transform.rotation = Quaternion.Euler(0f, -43f, 0f);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = world.gameObject;
            Debug.Log("Rushed Crops: saved Holotna open world, two bridges, four landmarks and a day/night cycle.");
        }

        private static void MakeMaterials()
        {
            Mat("Meadow", new Color(0.43f, 0.52f, 0.22f));
            Mat("MeadowLight", new Color(0.49f, 0.57f, 0.26f));
            Mat("MeadowShade", new Color(0.37f, 0.47f, 0.21f));
            Mat("MeadowGold", new Color(0.53f, 0.56f, 0.28f));
            Mat("PathShoulder", new Color(0.55f, 0.46f, 0.27f));
            Mat("Path", new Color(0.66f, 0.53f, 0.34f));
            Mat("Gravel", new Color(0.57f, 0.55f, 0.46f));
            Mat("Water", new Color(0.15f, 0.48f, 0.57f));
            Mat("WaterLight", new Color(0.25f, 0.61f, 0.65f));
            Mat("Foam", new Color(0.68f, 0.85f, 0.77f));
            Mat("Wood", new Color(0.30f, 0.18f, 0.10f));
            Mat("Plank", new Color(0.49f, 0.31f, 0.17f));
            Mat("CanvasCream", new Color(0.92f, 0.83f, 0.62f));
            Mat("CanvasRed", new Color(0.53f, 0.19f, 0.12f));
            Mat("SignText", new Color(0.18f, 0.12f, 0.06f));
            Mat("Moon", new Color(0.76f, 0.84f, 1f), true);
        }

        private static Material Mat(string name, Color colour, bool emissive = false)
        {
            string path = Generated + "Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = colour;
            string texture = name.StartsWith("Meadow") ? "Ground01A_ALB" :
                name == "Path" || name == "PathShoulder" ? "Ground02A_ALB" : name == "Gravel" ? "Ground03A_ALB" : null;
            if (texture != null)
            {
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Holotna/Medieval Rural/Terrain/" + texture + ".png"));
                material.color = colour * 1.75f;
            }
            material.SetFloat("_Smoothness", name.StartsWith("Water") ? 0.65f : 0f);
            material.enableInstancing = true;
            if (emissive) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", colour * 1.4f); }
            Materials[name] = material;
            return material;
        }

        private static void BuildGround(Transform parent)
        {
            const int columns = 104, rows = 88;
            var vertices = new List<Vector3>();
            var triangles = new[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            for (int z = 0; z < rows; z++)
            for (int x = 0; x < columns; x++)
            {
                float px = -104f + x * 2f, pz = -88f + z * 2f;
                Vector3 a = P(px, pz), b = P(px + 2f, pz), c = P(px, pz + 2f), d = P(px + 2f, pz + 2f);
                float shade = Mathf.PerlinNoise((px + 190f) * 0.04f, (pz + 210f) * 0.04f);
                int index = shade < 0.35f ? 2 : shade > 0.68f ? 3 : shade > 0.5f ? 1 : 0;
                AddTriangle(vertices, triangles[index], a, c, b);
                AddTriangle(vertices, triangles[index], b, c, d);
            }
            var mesh = new Mesh { name = "Farm valley terrain" };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
            AddWorldUVs(mesh,vertices);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            SaveMesh(mesh, "ValleyTerrain");
            var ground = MeshObject("Walkable valley", parent, mesh, new[] { Materials["Meadow"], Materials["MeadowLight"], Materials["MeadowShade"], Materials["MeadowGold"] });
            ground.layer = GroundLayer;
            ground.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        private static void BuildPaths(Transform parent)
        {
            for (int i = 0; i < Routes.Length; i++)
            {
                Ribbon("Trail shoulder " + i, Routes[i], 2.8f, parent, Materials["PathShoulder"], 0.07f);
                Ribbon("Dirt trail " + i, Routes[i], 1.95f, parent, Materials["Path"], 0.10f);
            }
            Clearing("Barn yard", Barn + V(0,-4), 13f, 18f, parent, Materials["Path"], 0.06f);
            Clearing("Garden soil surround", Garden, 13f, 12f, parent, Materials["PathShoulder"], 0.07f);
            Clearing("Market crossroads", Market, 11f, 11f, parent, Materials["Path"], 0.06f);
            Clearing("Quarry gravel", Quarry, 16f, 15f, parent, Materials["Gravel"], 0.06f);
            Clearing("Timber work yard", Logging, 15f, 12f, parent, Materials["PathShoulder"], 0.06f);
        }

        private static void BuildRiver(Transform parent)
        {
            Ribbon("Turquoise river", River, 4.0f, parent, Materials["Water"], -0.62f, true);
            Ribbon("River current", River, 1.55f, parent, Materials["WaterLight"], -0.60f, true);
            for (int i = 0; i < River.Length - 1; i++)
            {
                Vector2 direction = (River[i + 1] - River[i]).normalized;
                Vector2 side = new Vector2(-direction.y, direction.x);
                float length = Vector2.Distance(River[i], River[i + 1]);
                for (float t = 1; t < length; t += 2.6f)
                {
                    Vector2 centre = River[i] + direction * t;
                    foreach (int sign in new[] { -1, 1 })
                    {
                        Vector2 bank = centre + side * sign * R(4.4f, 6.7f);
                        Prefab("Rocks/Stone0" + random.Next(1,4), parent, bank, R(0f,360f), R(0.6f,1.55f), -0.2f);
                        if (random.NextDouble() < 0.5) Prefab("Vegetation/Cattail01", parent, bank + side * sign * 1f, R(0f,360f), R(0.9f,1.6f));
                    }
                    if (random.NextDouble() < 0.4)
                    {
                        var ripple = Box("Water ripple", parent, new Vector3(centre.x, -0.58f, centre.y), new Vector3(R(0.5f,1.5f),0.015f,0.08f), Materials["Foam"], false);
                        ripple.transform.rotation = Quaternion.Euler(0f, -Mathf.Atan2(direction.y,direction.x) * Mathf.Rad2Deg, 0f);
                    }
                }
            }
            Bridge(parent, V(-66,51), V(-0.53f,0.85f), "Willow bridge");
            Bridge(parent, V(76.6f,-28), V(1,0), "Timber bridge");
            Prefab("Transport/Rowboat01", parent, V(-87,35), 65f, 1.4f, -0.2f);
        }

        private static void Bridge(Transform parent, Vector2 centre, Vector2 forward, string name)
        {
            Transform bridge = new GameObject(name).transform;
            bridge.SetParent(parent, false);
            bridge.position = new Vector3(centre.x,0f,centre.y);
            bridge.rotation = Quaternion.LookRotation(new Vector3(forward.x,0f,forward.y));
            const float halfLength = 8f;
            for (int i = 0; i < 40; i++)
            {
                float z = -halfLength + 0.2f + i * 0.4f;
                float y = 0.05f + Mathf.Sin((z + halfLength) / (halfLength * 2f) * Mathf.PI) * 0.30f;
                LocalBox("Bridge plank", bridge, new Vector3(0,y,z), new Vector3(3.6f,0.15f,0.39f), Materials["Plank"], true);
            }
            foreach (float x in new[] { -1.72f, 1.72f })
            {
                for (float z = -7.5f; z <= 7.5f; z += 2.5f)
                    LocalBox("Timber post", bridge, new Vector3(x,0.3f,z), new Vector3(0.17f,2.1f,0.17f), Materials["Wood"], true);
                LocalBox("Handrail", bridge, new Vector3(x,1.1f,0), new Vector3(0.13f,0.15f,16f), Materials["Plank"], true);
                LocalBox("Low rail", bridge, new Vector3(x,0.6f,0), new Vector3(0.10f,0.12f,16f), Materials["Wood"], true);
            }
        }

        private static void BuildBarn(Transform parent)
        {
            var barn = Prefab("Barn01", parent, Barn, 0f, 1.7f);
            barn.name = "Inherited abandoned barn";
            // Missing roof sections and yard debris establish the restoration starting point.
            int roof = 0;
            foreach (Transform child in barn.GetComponentsInChildren<Transform>())
                if (child.name.StartsWith("RoofCenter") && ++roof % 7 == 0) child.gameObject.SetActive(false);
            Prefab("Other/Ladder01", parent, Barn + V(7,-4), -18f, 1.5f);
            Prefab("Transport/Wheelbarrow01", parent, Barn + V(7,-10), 28f, 1.4f);
            Prefab("Containers/Barrel01", parent, Barn + V(-7,-8), 0f, 1.6f);
            Prefab("Containers/Crate01", parent, Barn + V(7,-6), 15f, 1.5f);
            Prefab("Wood/FirewoodPile02", parent, Barn + V(-6,-7), 0f, 2f);
            Prefab("Tools/Shovel01", parent, Barn + V(6,-8), 15f, 1.5f);
            for (int i=0; i<5; i++)
            {
                Vector2 p = Barn + V(R(6f,10f),R(-2f,5f));
                var plank=Box("Fallen repair timber",parent,At(p,0.16f),new Vector3(0.24f,0.12f,R(1.5f,3.2f)),Materials["Plank"],false);
                plank.transform.rotation=Quaternion.Euler(0f,R(-45f,45f),R(-5f,5f));
            }
            for (int row=0;row<2;row++)
            for (int col=0;col<3;col++)
            {
                Vector2 plot = Garden + V((col-1)*6.4f,(row-0.5f)*7.6f);
                var patch=Prefab("Patch02",parent,plot,0f,1f);
                patch.name="Planting plot "+(row*3+col+1);
                HideStockCrops(patch.transform);
            }
            FenceLine(parent, Garden+V(-13,-11), Garden+V(13,-11));
            FenceLine(parent, Garden+V(-13,-11), Garden+V(-13,11));
            FenceLine(parent, Garden+V(13,-11), Garden+V(13,-3));
            FenceLine(parent, Garden+V(13,3), Garden+V(13,11));
            Lantern(parent, Barn+V(-6,-10), 0f);
            Lantern(parent, Garden+V(13,2),90f);
        }

        [MenuItem("Rushed Crops/World/Clear farming plots")]
        public static void ClearFarmingPlots()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Clear the saved plots outside Play.");
            var garden=GameObject.Find(RootName).transform.Find("03_BarnAndPlantingPlots");
            var remove=new List<GameObject>();
            foreach(Transform child in garden)
            {
                if(child.name.StartsWith("Planting plot"))HideStockCrops(child);
                else if(child.name.StartsWith("CarrotPlant")||child.name.StartsWith("WheatPlant"))remove.Add(child.gameObject);
            }
            foreach(GameObject crop in remove)Undo.DestroyObjectImmediate(crop);
            EditorSceneManager.MarkSceneDirty(garden.gameObject.scene);
            Debug.Log("Cleared all six farming plots; only empty soil beds remain.");
        }

        private static void HideStockCrops(Transform plot)
        {
            // The imported patch includes planted beets. Disable only those nested crop roots,
            // retaining the soil, original pack asset, and prefab connection.
            foreach(LODGroup lod in plot.GetComponentsInChildren<LODGroup>(true))
            {
                if(!lod.name.Contains("Plant"))continue;
                Undo.RecordObject(lod.gameObject,"Clear stock planting");
                lod.gameObject.SetActive(false);
            }
        }

        private static void BuildMarket(Transform parent)
        {
            Prefab("Storage01",parent,V(-5,7),180f,1.2f);
            Transform stall = new GameObject("Merchant stall").transform;
            stall.SetParent(parent,false);
            stall.position=At(V(2,0),0f);
            for(int i=0;i<4;i++)
                LocalBox("Awning post",stall,new Vector3(i%2==0?-3f:3f,1.6f,i<2?-1.5f:2f),new Vector3(0.16f,3.2f,0.16f),Materials["Wood"],true);
            for(int i=0;i<10;i++)
            {
                var panel=LocalBox("Striped market awning",stall,new Vector3(-2.88f+i*0.64f,3.15f,0.25f),new Vector3(0.64f,0.09f,4.2f),Materials[i%2==0?"CanvasCream":"CanvasRed"],false);
                panel.transform.localRotation=Quaternion.Euler(-9f,0f,0f);
                LocalBox("Awning valance",stall,new Vector3(-2.88f+i*0.64f,2.68f,-1.82f),new Vector3(0.64f,0.4f,0.07f),Materials[i%2==0?"CanvasCream":"CanvasRed"],false);
            }
            Prefab("Other/Table01",parent,V(2,-0.5f),0f,2.2f);
            Prefab("Containers/CrateOpen01",parent,V(-2,-2),-15f,1.5f);
            Prefab("Containers/CrateOpen02",parent,V(6,-1),25f,1.5f);
            Prefab("Containers/Barrel01",parent,V(6,2),0f,1.6f);
            Prefab("Transport/Cart01",parent,V(8,7),-30f,1.35f);
            for(int i=0;i<7;i++) Prefab("Crops/Carrot01",parent,V(0.8f+i*0.38f,-0.5f),R(-20,20),1f,1.38f);
            Prefab("Vegetation/Oak02",parent,V(-12,9),-15f,1.8f);
            Lantern(parent,V(-5,-4),0f);
            Lantern(parent,V(7,-4),0f);
        }

        private static void BuildQuarry(Transform parent)
        {
            for(int i=0;i<7;i++)
            {
                float angle=(i*25f-30f)*Mathf.Deg2Rad;
                Vector2 p=Quarry+V(Mathf.Cos(angle)*14f,Mathf.Sin(angle)*14f);
                Prefab("Rocks/QuarryFace01",parent,p,-i*25f, R(1.35f,2f),-0.4f);
            }
            Prefab("Shed01",parent,Quarry+V(-10,5),110f,1.2f);
            Prefab("Transport/Cart01",parent,Quarry+V(-5,-4),20f,1.5f);
            Prefab("Tools/Pickaxe01",parent,Quarry+V(0,-4),-30f,1.5f);
            for(int i=0;i<22;i++)
            {
                Vector2 p=Quarry+V(R(-6,10),R(-6,9));
                if(Vector2.Distance(p,Quarry+V(-5,-7))<3f)continue;
                Prefab(i%3==0?"Rocks/Block02":"Rocks/Stone03",parent,p,R(0,360),R(0.8f,2f),0.1f);
            }
            Prefab("Containers/Crate01",parent,Quarry+V(-9,1),0f,1.3f);
            Lantern(parent,Quarry+V(-10,-5),20f);
        }

        private static void BuildLogging(Transform parent)
        {
            Prefab("Shed01",parent,Logging+V(6,6),180f,1.35f);
            Prefab("Wood/LogPile03",parent,Logging+V(-6,2),90f,3.8f);
            Prefab("Wood/LogPile02",parent,Logging+V(-7,5),85f,3.2f);
            Prefab("Other/Sawbuck01",parent,Logging+V(1,-2),25f,1.9f);
            Prefab("Wood/ChoppingBlock01",parent,Logging+V(5,-4),0f,2f);
            Prefab("Tools/Axe01",parent,Logging+V(5,-4),15f,1.3f,0.7f);
            Prefab("Transport/Cart01",parent,Logging+V(-5,-7),100f,1.5f);
            Prefab("Wood/FirewoodPile03",parent,Logging+V(8,3),0f,2.5f);
            for(int i=0;i<12;i++) Prefab("Vegetation/PineStump02",parent,Logging+V(R(-9,10),R(-8,9)),R(0,360),R(1.3f,2.1f));
            for(int i=0;i<4;i++) Prefab("Wood/Log02",parent,Logging+V(R(-5,6),R(-6,6)),R(0,360),R(2f,3.5f));
            Lantern(parent,Logging+V(1,4),0f);
        }

        private static void BuildForest(Transform parent)
        {
            string[] trees={"Oak01","Oak02","Pine02","Spruce02","Pine01"};
            var accepted=new List<Vector2>();
            for(int attempt=0;attempt<3500 && accepted.Count<480;attempt++)
            {
                Vector2 p=V(R(-101,101),R(-85,85));
                if(InClearing(p,3f) || PathDistance(p)<6.1f || Distance(p,River)<8.5f)continue;
                bool crowded=false;
                foreach(Vector2 other in accepted) if((p-other).sqrMagnitude<20f){crowded=true;break;}
                if(crowded)continue;
                accepted.Add(p);
                string tree=trees[random.Next(trees.Length)];
                Prefab("Vegetation/"+tree,parent,p,R(0,360),R(1.25f,2.15f));
            }
            for(int i=0;i<145;i++)
            {
                Vector2 p=V(R(-99,99),R(-83,83));
                if(InClearing(p,2f)||PathDistance(p)<5f||Distance(p,River)<8f)continue;
                Prefab("Rocks/Stone0"+random.Next(1,4),parent,p,R(0,360),R(1.2f,3.7f),-0.18f);
            }
            string[] small={"Grass01","Grass02","Fern01","Flowers01","Flowers02","Bush01"};
            for(int i=0;i<1500;i++)
            {
                Vector2 p=V(R(-99,99),R(-83,83));
                if(InClearing(p,0.5f)||PathDistance(p)<3.5f||Distance(p,River)<6.5f)continue;
                Prefab("Vegetation/"+small[random.Next(small.Length)],parent,p,R(0,360),R(0.7f,1.5f),0f,false);
            }
            // Frame clearings with flowers and grasses, leaving their entrances clear.
            foreach(Vector2 centre in new[]{Barn,Garden,Market,Quarry,Logging})
            for(int i=0;i<18;i++)
            {
                float a=R(0,Mathf.PI*2f); Vector2 p=centre+V(Mathf.Cos(a),Mathf.Sin(a))*R(15f,19f);
                if(PathDistance(p)<4f||Distance(p,River)<7f||InClearing(p,0))continue;
                Prefab("Vegetation/Flowers0"+random.Next(1,4),parent,p,R(0,360),R(1f,1.6f),0f,false);
            }
        }

        private static void BuildSigns(Transform parent)
        {
            Sign(parent,V(-18,-32),"OLD BARN\nPLANTING PLOTS",-25f);
            Sign(parent,V(-9,-5),"MERCHANT",0f);
            Sign(parent,V(15,24),"STONE QUARRY  >",-20f);
            Sign(parent,V(20,-31),"TIMBER CAMP  >",-30f);
            Sign(parent,V(-54,32),"WILLOW BRIDGE",10f);
            Sign(parent,V(63,-22),"RIVERSIDE",0f);
        }

        private static void Sign(Transform parent, Vector2 position, string message, float yaw)
        {
            Transform sign=new GameObject("Sign - "+message.Replace("\n"," / ")).transform;
            sign.SetParent(parent,false);sign.position=At(position,0);sign.rotation=Quaternion.Euler(0,yaw,0);
            LocalBox("Post",sign,new Vector3(0,1.1f,0),new Vector3(0.15f,2.2f,0.15f),Materials["Wood"],true);
            LocalBox("Wooden board",sign,new Vector3(0,2f,0),new Vector3(3f,0.8f,0.16f),Materials["Plank"],true);
            LocalBox("Painted face",sign,new Vector3(0,2f,-0.09f),new Vector3(2.82f,0.65f,0.02f),Materials["CanvasCream"],false);
            var text=new GameObject("Directions").AddComponent<TextMesh>();
            text.transform.SetParent(sign,false);text.transform.localPosition=new Vector3(0,2f,-0.108f);
            text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.text=message;
            text.fontSize=64;text.characterSize=0.052f;text.color=new Color(0.17f,0.10f,0.04f);
        }

        private static void BuildBoundaries(Transform parent)
        {
            foreach(float x in new[]{-103.5f,103.5f})
                Boundary(parent,new Vector3(x,10f,0),new Vector3(1,30,176));
            foreach(float z in new[]{-87.5f,87.5f})
                Boundary(parent,new Vector3(0,10f,z),new Vector3(208,30,1));
            // A rock/forest rim gives the small open world a readable natural edge.
            for(int i=0;i<70;i++)
            {
                float a=i*Mathf.PI*2f/70f;
                Vector2 p=V(Mathf.Cos(a)*101f,Mathf.Sin(a)*85f);
                if(Distance(p,River)<8f||PathDistance(p)<5f)continue;
                Prefab("Rocks/Stone03",parent,p,R(0,360),R(4f,8f),-0.4f);
            }
        }

        private static void BuildEnvironment()
        {
            var environment=new GameObject("WORLD_Environment");
            var cycle=environment.AddComponent<EnvironmentCycle>();
            Light sun=GameObject.Find("Directional Light").GetComponent<Light>();
            sun.shadows=LightShadows.Soft;sun.shadowStrength=0.8f;
            var moonObject=new GameObject("Moon Light");moonObject.transform.SetParent(environment.transform,false);
            var moon=moonObject.AddComponent<Light>();moon.type=LightType.Directional;moon.color=new Color(0.51f,0.67f,1f);moon.shadows=LightShadows.None;
            var orb=GameObject.CreatePrimitive(PrimitiveType.Sphere);orb.name="Moon disc";Object.DestroyImmediate(orb.GetComponent<Collider>());
            orb.transform.SetParent(moon.transform,false);orb.transform.localPosition=new Vector3(0,0,-190);orb.transform.localScale=Vector3.one*5f;
            orb.GetComponent<Renderer>().sharedMaterial=Materials["Moon"];orb.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            string skyPath=Generated+"Materials/FarmSky.mat";
            var sky=new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize",0.045f);sky.SetFloat("_AtmosphereThickness",0.85f);sky.SetFloat("_Exposure",1.15f);
            sky.SetColor("_SkyTint",new Color(0.47f,0.63f,0.77f));sky.SetColor("_GroundColor",new Color(0.37f,0.42f,0.26f));
            var existingSky=AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if(existingSky==null)AssetDatabase.CreateAsset(sky,skyPath);else{EditorUtility.CopySerialized(sky,existingSky);Object.DestroyImmediate(sky);sky=existingSky;}
            RenderSettings.skybox=sky;
            var serialized=new SerializedObject(cycle);
            serialized.FindProperty("sun").objectReferenceValue=sun;
            serialized.FindProperty("moon").objectReferenceValue=moon;
            var lights=serialized.FindProperty("lanterns");lights.arraySize=Lanterns.Count;
            for(int i=0;i<Lanterns.Count;i++)lights.GetArrayElementAtIndex(i).objectReferenceValue=Lanterns[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            cycle.SetHour(9f);
            BuildClockHud(cycle);
        }

        private static void BuildClockHud(EnvironmentCycle cycle)
        {
            var root=new GameObject("HUD_WorldClock",typeof(Canvas),typeof(CanvasScaler));
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=0.5f;
            var panel=new GameObject("Clock panel",typeof(RectTransform),typeof(Image));panel.transform.SetParent(root.transform,false);
            var rect=panel.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(0.5f,1);rect.pivot=new Vector2(0.5f,1);rect.anchoredPosition=new Vector2(0,-30);rect.sizeDelta=new Vector2(230,80);
            panel.GetComponent<Image>().color=new Color(0.09f,0.12f,0.10f,0.8f);panel.GetComponent<Image>().raycastTarget=false;
            var label=new GameObject("Day and time",typeof(RectTransform),typeof(Text));label.transform.SetParent(panel.transform,false);
            var lr=label.GetComponent<RectTransform>();lr.anchorMin=Vector2.zero;lr.anchorMax=Vector2.one;lr.offsetMin=new Vector2(12,8);lr.offsetMax=new Vector2(-12,-8);
            var text=label.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=22;text.alignment=TextAnchor.MiddleCenter;text.color=new Color(1f,0.94f,0.77f);text.text="DAY 1   09:00\nMORNING";text.raycastTarget=false;
            var hud=root.AddComponent<EnvironmentClockHud>();var serialized=new SerializedObject(hud);serialized.FindProperty("environment").objectReferenceValue=cycle;serialized.FindProperty("label").objectReferenceValue=text;serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Lantern(Transform parent,Vector2 position,float yaw)
        {
            Transform post=new GameObject("Dusk lantern").transform;post.SetParent(parent,false);post.position=At(position,0);post.rotation=Quaternion.Euler(0,yaw,0);
            LocalBox("Lantern post",post,new Vector3(0,1.5f,0),new Vector3(0.15f,3f,0.15f),Materials["Wood"],true);
            LocalBox("Hanging beam",post,new Vector3(0,2.9f,0.3f),new Vector3(0.13f,0.13f,0.85f),Materials["Plank"],false);
            var model=Prefab("Other/Lantern01",post,Vector2.zero,0,1.5f,0,false);
            model.transform.localPosition=new Vector3(0,2.85f,0.58f);model.transform.localRotation=Quaternion.identity;
            var go=new GameObject("Warm lantern light");go.transform.SetParent(post,false);go.transform.localPosition=new Vector3(0,2.4f,0.58f);
            var light=go.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1f,0.57f,0.22f);light.range=10f;light.intensity=3f;light.shadows=LightShadows.None;light.enabled=false;Lanterns.Add(light);
        }

        private static void FenceLine(Transform parent,Vector2 from,Vector2 to)
        {
            Vector2 direction=(to-from).normalized;float length=Vector2.Distance(from,to);int count=Mathf.RoundToInt(length/1.85f);
            for(int i=0;i<count;i++) Prefab("Other/Fence01",parent,Vector2.Lerp(from,to,(i+0.5f)/count),-Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg,1f);
        }

        private static GameObject Prefab(string relative,Transform parent,Vector2 position,float yaw,float scale,float offset=0f,bool collision=true)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Pack+relative+".prefab");
            if(asset==null)throw new InvalidOperationException("Missing Holotna prefab: "+relative);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);
            go.transform.position=At(position,offset);go.transform.rotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=Vector3.one*scale;
            foreach(var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats=renderer.sharedMaterials;
                for(int i=0;i<mats.Length;i++)
                {
                    if(mats[i]==null)continue;
                    var coloured=AssetDatabase.LoadAssetAtPath<Material>("Assets/Holotna/Medieval Rural/Materials/Colored/"+mats[i].name+".mat");
                    if(coloured!=null)mats[i]=coloured;
                }
                renderer.sharedMaterials=mats;
            }
            foreach(var collider in go.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled=collision;
                collider.gameObject.layer=GroundLayer;
            }
            foreach(var light in go.GetComponentsInChildren<Light>(true))
            {
                light.enabled=false;
                light.shadows=LightShadows.None;
                Lanterns.Add(light);
            }
            return go;
        }

        private static void Clearing(string name,Vector2 centre,float radiusX,float radiusZ,Transform parent,Material material,float offset)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            const int rings=7,segments=48;
            for(int r=0;r<rings;r++)for(int s=0;s<segments;s++)
            {
                float a=s*Mathf.PI*2/segments,b=(s+1)*Mathf.PI*2/segments;
                float inner=r/(float)rings,outer=(r+1f)/rings;
                float edgeA=1f+Mathf.Sin(a*3f)*0.06f+Mathf.Cos(a*5f)*0.035f;
                float edgeB=1f+Mathf.Sin(b*3f)*0.06f+Mathf.Cos(b*5f)*0.035f;
                Vector3 v1=At(centre+V(Mathf.Cos(a)*radiusX,Mathf.Sin(a)*radiusZ)*inner*edgeA,offset);
                Vector3 v2=At(centre+V(Mathf.Cos(b)*radiusX,Mathf.Sin(b)*radiusZ)*inner*edgeB,offset);
                Vector3 v3=At(centre+V(Mathf.Cos(a)*radiusX,Mathf.Sin(a)*radiusZ)*outer*edgeA,offset);
                Vector3 v4=At(centre+V(Mathf.Cos(b)*radiusX,Mathf.Sin(b)*radiusZ)*outer*edgeB,offset);
                AddTriangle(vertices,triangles,v1,v2,v3);AddTriangle(vertices,triangles,v2,v4,v3);
            }
            MakeMesh(name,parent,vertices,triangles,material);
        }

        private static void Ribbon(string name,Vector2[] points,float halfWidth,Transform parent,Material material,float height,bool flat=false)
        {
            var samples=new List<Vector2>();
            for(int i=0;i<points.Length-1;i++)
            {
                int count=Mathf.CeilToInt(Vector2.Distance(points[i],points[i+1]));
                for(int j=0;j<count;j++)samples.Add(Vector2.Lerp(points[i],points[i+1],j/(float)count));
            }
            samples.Add(points[points.Length-1]);
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<samples.Count-1;i++)
            {
                if(!flat && (Distance(samples[i],River)<5f||Distance(samples[i+1],River)<5f))continue;
                Vector2 direction=(samples[Mathf.Min(i+1,samples.Count-1)]-samples[Mathf.Max(i-1,0)]).normalized;
                Vector2 side=V(-direction.y,direction.x)*halfWidth;
                Vector2 endDirection=(samples[Mathf.Min(i+2,samples.Count-1)]-samples[i]).normalized;
                Vector2 endSide=V(-endDirection.y,endDirection.x)*halfWidth;
                Vector3 a=At(samples[i]-side,height),b=At(samples[i]+side,height),c=At(samples[i+1]-endSide,height),d=At(samples[i+1]+endSide,height);
                if(flat){a.y=b.y=c.y=d.y=height;}
                AddTriangle(vertices,triangles,a,b,c);AddTriangle(vertices,triangles,b,d,c);
            }
            MakeMesh(name,parent,vertices,triangles,material);
        }

        private static void MakeMesh(string name,Transform parent,List<Vector3> vertices,List<int> triangles,Material material)
        {
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);AddWorldUVs(mesh,vertices);mesh.RecalculateNormals();mesh.RecalculateBounds();SaveMesh(mesh,name.Replace(" ",""));MeshObject(name,parent,mesh,new[]{material});
        }
        private static void AddWorldUVs(Mesh mesh,List<Vector3> vertices)
        {
            var uv=new List<Vector2>(vertices.Count);
            foreach(Vector3 vertex in vertices)uv.Add(new Vector2(vertex.x/5f,vertex.z/5f));
            mesh.SetUVs(0,uv);
        }
        private static GameObject MeshObject(string name,Transform parent,Mesh mesh,Material[] materials)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<Renderer>().sharedMaterials=materials;return go;
        }
        private static void SaveMesh(Mesh mesh,string name) { AssetDatabase.CreateAsset(mesh,Generated+"Meshes/"+name+".asset"); }
        private static void AddTriangle(List<Vector3> vertices,List<int> triangles,Vector3 a,Vector3 b,Vector3 c)
        {
            int start=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);
        }
        private static GameObject Box(string name,Transform parent,Vector3 position,Vector3 scale,Material material,bool collision)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.position=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;
            if(!collision)Object.DestroyImmediate(go.GetComponent<Collider>());else go.layer=GroundLayer;return go;
        }
        private static GameObject LocalBox(string name,Transform parent,Vector3 position,Vector3 scale,Material material,bool collision)
        {
            var go=Box(name,parent,Vector3.zero,scale,material,collision);go.transform.localPosition=position;go.transform.localRotation=Quaternion.identity;return go;
        }
        private static void Boundary(Transform parent,Vector3 position,Vector3 size)
        {
            var go=new GameObject("Map edge collider");go.transform.SetParent(parent,false);go.transform.position=position;go.AddComponent<BoxCollider>().size=size;
        }
        private static Transform Group(string name){var go=new GameObject(name);go.transform.SetParent(world,false);return go.transform;}
        private static Vector2 V(float x,float z)=>new Vector2(x,z);
        private static Vector3 P(float x,float z)=>new Vector3(x,Height(x,z),z);
        private static Vector3 At(Vector2 p,float offset)=>new Vector3(p.x,Height(p.x,p.y)+offset,p.y);
        private static float R(float min,float max)=>min+(float)random.NextDouble()*(max-min);
        private static Vector2[][] SmoothRoutes(Vector2[][] routes)
        {
            var result=new Vector2[routes.Length][];
            for(int i=0;i<routes.Length;i++)result[i]=Smooth(routes[i]);
            return result;
        }
        private static Vector2[] Smooth(Vector2[] points)
        {
            var result=new List<Vector2>();
            for(int i=0;i<points.Length-1;i++)
            {
                Vector2 a=points[Mathf.Max(0,i-1)],b=points[i],c=points[i+1],d=points[Mathf.Min(points.Length-1,i+2)];
                int steps=Mathf.Max(2,Mathf.CeilToInt(Vector2.Distance(b,c)/2f));
                for(int n=0;n<steps;n++)
                {
                    float t=n/(float)steps;
                    result.Add(0.5f*((2f*b)+(-a+c)*t+(2f*a-5f*b+4f*c-d)*t*t+(-a+3f*b-3f*c+d)*t*t*t));
                }
            }
            result.Add(points[points.Length-1]);
            return result.ToArray();
        }
        private static float Height(float x,float z)
        {
            Vector2 p=V(x,z);
            float edge=Mathf.Max(Mathf.Abs(x)/104f,Mathf.Abs(z)/88f);
            float height=(Mathf.PerlinNoise((x+200)*0.022f,(z+200)*0.022f)-0.5f)*3f;
            height+=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.72f,1f,edge))*8f;
            float clearing=1f;
            foreach(Vector2 centre in new[]{Barn,Garden,Market,Quarry,Logging})
            {
                float radius=centre==Barn?16f:centre==Garden?17f:centre==Quarry?18f:15f;
                clearing=Mathf.Min(clearing,Mathf.SmoothStep(0,1,Mathf.InverseLerp(radius,radius+8f,Vector2.Distance(p,centre))));
            }
            clearing=Mathf.Min(clearing,Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.5f,8f,PathDistance(p))));
            height*=clearing;
            float riverDistance=Distance(p,River);
            if(riverDistance<9f)height=Mathf.Min(height,Mathf.Lerp(-1.7f,0.25f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(3f,9f,riverDistance))));
            return height;
        }
        private static bool InClearing(Vector2 p,float extra)
        {
            return Vector2.Distance(p,Barn)<16f+extra||Vector2.Distance(p,Garden)<18f+extra||Vector2.Distance(p,Market)<14f+extra||Vector2.Distance(p,Quarry)<18f+extra||Vector2.Distance(p,Logging)<16f+extra;
        }
        private static float PathDistance(Vector2 p){float nearest=float.MaxValue;foreach(var route in Routes)nearest=Mathf.Min(nearest,Distance(p,route));return nearest;}
        private static float Distance(Vector2 p,Vector2[] points)
        {
            float nearest=float.MaxValue;
            for(int i=0;i<points.Length-1;i++)
            {
                Vector2 segment=points[i+1]-points[i];float t=Mathf.Clamp01(Vector2.Dot(p-points[i],segment)/segment.sqrMagnitude);
                nearest=Mathf.Min(nearest,Vector2.Distance(p,points[i]+segment*t));
            }
            return nearest;
        }
        private static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;int split=path.LastIndexOf('/');EnsureFolder(path.Substring(0,split));AssetDatabase.CreateFolder(path.Substring(0,split),path.Substring(split+1));
        }
    }
}
