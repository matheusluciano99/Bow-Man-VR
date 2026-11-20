using System.Collections.Generic;
using System.IO;
using BowMan;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class DuelEnvironmentBuilder
{
    private const string Art = "Assets/ThirdParty/PolyHaven";
    private const string Output = "Assets/_Game/Resources/Arenas";
    private const string Geometry = "Assets/_Game/Models/Arenas";
    private const string Surfaces = "Assets/_Game/Materials/Arenas";

    // Bakes three inspector-ready arenas, their shared materials and their scenery meshes.
    [MenuItem("Bow Man VR/Arenas/Rebuild medieval environments")]
    public static void Build()
    {
        foreach (var path in new[] { Output, Geometry, Surfaces }) Directory.CreateDirectory(path);
        AssetDatabase.Refresh();
        foreach (ArenaSetting setting in System.Enum.GetValues(typeof(ArenaSetting)))
            new Arena(setting).Save();
        AssetDatabase.SaveAssets();
    }

    // Rebuilds the asset in place so existing prefab and material references keep their GUIDs.
    private static T Save<T>(T asset, string path) where T : Object
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing == null) { AssetDatabase.CreateAsset(asset, path); return asset; }
        EditorUtility.CopySerialized(asset, existing);
        Object.DestroyImmediate(asset);
        return existing;
    }

    private sealed class Arena
    {
        private readonly ArenaSetting setting;
        private readonly GameObject root;
        private readonly Dictionary<Material, List<CombineInstance>> surfaces = new();
        private readonly Dictionary<string, Material> materials = new();
        private readonly List<Mesh> temporary = new();
        private readonly Mesh cube;
        private readonly System.Random variation = new(1597);

        // Keeps construction and deterministic art variation local to one authored setting.
        public Arena(ArenaSetting setting)
        {
            this.setting = setting;
            root = new GameObject(setting.ToString());
            var reference = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube = reference.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(reference);
        }

        // Creates tiled ground, perimeter architecture and a setting-specific panorama.
        public void Save()
        {
            try
            {
                var ground = setting switch
                {
                    ArenaSetting.CastleCourtyard => "cobblestone_floor_02",
                    ArenaSetting.WoodlandRange => "forest_ground_04",
                    _ => "brown_mud_03"
                };
                Box(new Vector3(0,-0.045f,0), new Vector3(90,0.11f,90), ground, true);
                switch (setting)
                {
                    case ArenaSetting.CastleCourtyard: Castle(); break;
                    case ArenaSetting.WoodlandRange: Woodland(); break;
                    case ArenaSetting.VillageYard: Village(); break;
                }
                foreach (var pair in surfaces)
                {
                    var mesh = new Mesh { name = setting + " " + pair.Key.name, indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(pair.Value.ToArray());
                    var child = new GameObject(pair.Key.name);
                    child.transform.SetParent(root.transform, false);
                    child.AddComponent<MeshFilter>().sharedMesh = DuelEnvironmentBuilder.Save(mesh, $"{Geometry}/{mesh.name}.asset");
                    child.AddComponent<MeshRenderer>().sharedMaterial = pair.Key;
                }
                var component = new SerializedObject(root.AddComponent<DuelEnvironment>());
                var sky = setting switch
                {
                    ArenaSetting.CastleCourtyard => "kloppenheim_02_puresky",
                    ArenaSetting.WoodlandRange => "forest_grove",
                    _ => "autumn_meadow"
                };
                var panorama = new Material(Shader.Find("Skybox/Panoramic")) { name = sky };
                var skyPath = $"{Art}/{sky}/{sky}_2k.hdr";
                var importer = (TextureImporter)AssetImporter.GetAtPath(skyPath);
                if (importer.textureShape != TextureImporterShape.Texture2D)
                {
                    importer.textureShape = TextureImporterShape.Texture2D;
                    importer.SaveAndReimport();
                }
                panorama.SetTexture("_MainTex", Texture(sky, sky + "_2k.hdr"));
                panorama.SetFloat("_Rotation", setting == ArenaSetting.WoodlandRange ? 110f : 32f);
                component.FindProperty("sky").objectReferenceValue = DuelEnvironmentBuilder.Save(panorama, $"{Surfaces}/{sky}.mat");
                component.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, $"{Output}/{setting}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
                foreach (var mesh in temporary) Object.DestroyImmediate(mesh);
            }
        }

        // Retrieves an imported Poly Haven map by its original filename.
        private static Texture2D Texture(string folder, string file) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{Art}/{folder}/{file}") ??
            throw new FileNotFoundException($"Missing environment texture {folder}/{file}");

        // Shares one rough, instanced surface for every occurrence of the same source texture.
        private Material Surface(string key)
        {
            if (materials.TryGetValue(key, out var result)) return result;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = key, enableInstancing = true };
            mat.SetFloat("_Smoothness", 0.09f);
            if (key == "iron" || key == "red cloth" || key == "blue cloth" || key == "shadow" || key == "straw")
            {
                mat.color = key switch { "iron" => new Color(.18f,.18f,.16f), "red cloth" => new Color(.42f,.085f,.07f),
                    "blue cloth" => new Color(.075f,.17f,.28f), "straw" => new Color(.64f,.48f,.25f), _ => new Color(.075f,.062f,.047f) };
            }
            else if (key.StartsWith("fir_sapling_medium"))
            {
                mat.mainTexture = Texture("fir_sapling_medium", key + ".png");
                mat.SetFloat("_Cull", 0); mat.SetFloat("_AlphaClip", 1); mat.SetFloat("_Cutoff", .27f);
                mat.EnableKeyword("_ALPHATEST_ON"); mat.SetOverrideTag("RenderType", "TransparentCutout"); mat.renderQueue = 2450;
            }
            else mat.mainTexture = Texture(key, key + "_diff_1k.jpg");
            result = DuelEnvironmentBuilder.Save(mat, $"{Surfaces}/{key}.mat");
            materials.Add(key,result);
            return result;
        }

        // Collects submeshes by shared surface so repeated masonry and timber render in batches.
        private void Add(Mesh mesh, Matrix4x4 transform, string surface, int submesh = 0)
        {
            var material = Surface(surface);
            if (!surfaces.TryGetValue(material, out var parts)) surfaces.Add(material, parts = new());
            parts.Add(new CombineInstance { mesh = mesh, transform = transform, subMeshIndex = submesh });
        }

        // Tiles masonry and timber to measured dimensions and optionally gives solid structures arrow collision.
        private void Box(Vector3 position, Vector3 size, string surface, bool solid = false, Quaternion rotation = default)
        {
            if (rotation == default) rotation = Quaternion.identity;
            var mesh = Object.Instantiate(cube);
            var verts = mesh.vertices;
            var normals = mesh.normals;
            var uv = new Vector2[verts.Length];
            for (var i = 0; i < verts.Length; i++)
            {
                var point = Vector3.Scale(verts[i], size);
                var n = normals[i];
                uv[i] = (Mathf.Abs(n.y) > .5f ? new Vector2(point.x, point.z) :
                    Mathf.Abs(n.x) > .5f ? new Vector2(point.z, point.y) : new Vector2(point.x, point.y)) * 0.55f;
            }
            mesh.uv = uv;
            temporary.Add(mesh);
            Add(mesh, Matrix4x4.TRS(position, rotation, size), surface);
            if (!solid) return;
            var barrier = new GameObject(surface + " collision");
            barrier.transform.SetParent(root.transform, false);
            barrier.transform.SetPositionAndRotation(position, rotation);
            barrier.AddComponent<BoxCollider>().size = size;
        }

        // Fits a square-section beam between two construction points.
        private void Beam(Vector3 a, Vector3 b, float thickness, string material = "medieval_wood")
        {
            var delta = b-a;
            Box((a+b)*.5f, new Vector3(thickness, thickness, delta.magnitude), material, false, Quaternion.LookRotation(delta));
        }

        // Places an imported scenery prop with shared source material and original surface UVs.
        private void Prop(string name, float x, float z, float scale, float yaw)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Art}/{name}/{name}.fbx");
            var placement = Matrix4x4.TRS(new Vector3(x,.015f,z), Quaternion.Euler(0,yaw,0), Vector3.one*scale);
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
                for (var i=0;i<filter.sharedMesh.subMeshCount;i++)
                    Add(filter.sharedMesh, placement * filter.transform.localToWorldMatrix, name, i);
        }

        // Builds four crenellated curtain walls, corner towers and timber gates across the firing lane.
        private void Castle()
        {
            const string stone = "castle_brick_01";
            for (var side=-1;side<=1;side+=2)
            {
                Box(new Vector3(side*12,2.25f,0), new Vector3(1.3f,4.5f,40),stone,true);
                for (var z=-19;z<=19;z+=2) Box(new Vector3(side*12,4.94f,z),new Vector3(1.35f,.88f,.95f),stone);
                for (var x=-1;x<=1;x+=2)
                {
                    var tower = new Vector3(x*11,0,side*18.5f);
                    Box(tower+Vector3.up*3.7f,new Vector3(4.5f,7.4f,4.5f),stone,true);
                    Box(tower+Vector3.up*7.15f,new Vector3(4.85f,.45f,4.85f),stone);
                    for (var t=-2;t<=2;t+=2)
                    {
                        Box(tower+new Vector3(t,7.8f,-2),new Vector3(.9f,1,.9f),stone);
                        Box(tower+new Vector3(t,7.8f,2),new Vector3(.9f,1,.9f),stone);
                    }
                    Box(tower+new Vector3(0,5.65f,-side*2.26f),new Vector3(.22f,1.05f,.06f),"shadow");
                    Box(new Vector3(x*6.65f,2.35f,side*19),new Vector3(8.7f,4.7f,1.25f),stone,true);
                    for (var j=3;j<=9;j+=2) Box(new Vector3(x*j,5.13f,side*19),new Vector3(.95f,.86f,1.3f),stone);
                    Box(new Vector3(x*1.1f,1.96f,side*19),new Vector3(2.14f,3.9f,.36f),"medieval_wood",true);
                    Box(new Vector3(x*4.4f,2.8f,side*18.32f),new Vector3(.76f,2.6f,.045f),side==1?"red cloth":"blue cloth");
                }
                Box(new Vector3(0,4.55f,side*19),new Vector3(4.7f,1.3f,1.25f),stone);
                Prop("wine_barrel_01",side*8,side*15,1.25f,36);
                Prop("wooden_crate_01",side*6.7f,side*15.5f,1.15f,16);
            }
        }

        // Uses two rendered profiles of a real fir with planted stumps and a timber range fence.
        private void Woodland()
        {
            var treeCard = new Mesh { name="Fir profile" };
            treeCard.vertices=new[]{new Vector3(-.5f,0,0),new Vector3(.5f,0,0),new Vector3(-.5f,1,0),new Vector3(.5f,1,0)};
            treeCard.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one};
            treeCard.triangles=new[]{0,2,1,1,2,3}; treeCard.RecalculateNormals(); temporary.Add(treeCard);
            for (var i=0;i<105;i++)
            {
                var angle=(float)variation.NextDouble()*360;
                var radial=Quaternion.Euler(0,angle,0)*Vector3.forward;
                var distance=12f+(float)variation.NextDouble()*25f;
                var pos=new Vector3(radial.x*distance,-.12f,radial.z*(distance+13f));
                var h=4.8f+(float)variation.NextDouble()*8;
                for(var k=0;k<2;k++)
                    Add(treeCard,Matrix4x4.TRS(pos,Quaternion.Euler(0,angle+k*90,0),new Vector3(h,h,1)),
                        k==0?"fir_sapling_medium_front":"fir_sapling_medium_side");
            }
            for(var side=-1;side<=1;side+=2)
            {
                Fence(side*7.4f,-16,16);
                Prop("tree_stump_01",side*6,side*10,1.1f,89*side);
                Prop("tree_stump_01",side*9,-side*12,1.5f,31*side);
                Target(side*3.9f,side*16.4f);
            }
        }

        // Makes the training yard readable from either seat with gabled timber-frame buildings and target stands.
        private void Village()
        {
            for(var side=-1;side<=1;side+=2)
            {
                House(side*10,side*11,6.1f,7.8f);
                House(-side*8.8f,side*18.8f,5.4f,6.5f);
                House(side*2.5f,side*24.5f,6.8f,6f);
                Fence(side*6.6f,-6,6);
                Prop("wine_barrel_01",side*5.8f,side*13,1.2f,43);
                Prop("wooden_crate_01",side*6.9f,side*12.4f,1.35f,18);
                Target(-side*3.8f,side*16.8f);
            }
        }

        // Places a clay-tile roof, exposed braces, recessed openings and a masonry chimney.
        private void House(float x,float z,float width,float depth)
        {
            var c=new Vector3(x,0,z);
            const float eave=3.6f, rise=2.2f;
            Box(c+Vector3.up*.29f,new Vector3(width+.22f,.58f,depth+.22f),"castle_brick_01");
            Box(c+Vector3.up*1.93f,new Vector3(width,3.35f,depth),"plastered_wall_02",true);
            for(var side=-1;side<=1;side+=2)
            {
                var front=c+new Vector3(0,0,side*(depth*.5f+.035f));
                for(var j=-1;j<=1;j++) Box(front+new Vector3(j*width*.49f,eave*.5f,0),new Vector3(.18f,eave,.16f),"medieval_wood");
                Box(front+Vector3.up*2.32f,new Vector3(width,.16f,.18f),"medieval_wood");
                Beam(front+new Vector3(-width*.47f,.65f,0),front+new Vector3(-width*.09f,2.24f,0),.14f);
                Box(front+new Vector3(width*.24f,1.22f,side*.05f),new Vector3(1.06f,2.45f,.12f),"medieval_wood");
                Box(front+new Vector3(-width*.24f,2.88f,side*.05f),new Vector3(.87f,.8f,.12f),"shadow");
                Box(c+new Vector3(side*width*.5f,2.3f,0),new Vector3(.18f,.17f,depth+.12f),"medieval_wood");
                for(var k=-1;k<=1;k++) Box(c+new Vector3(side*width*.5f,1.8f,k*depth*.45f),new Vector3(.18f,eave,.2f),"medieval_wood");
                var length=Mathf.Sqrt(width*width*.25f+rise*rise);
                var slope=Quaternion.AngleAxis(-side*Mathf.Atan2(rise,width*.5f)*Mathf.Rad2Deg,Vector3.forward);
                Box(c+new Vector3(side*width*.25f,eave+rise*.5f,0),new Vector3(length+.68f,.17f,depth+.85f),"clay_roof_tiles_02",false,slope);
                // Narrow timber courses fill the tapered gable below the roof ridge.
                for(var level=0;level<8;level++)
                    Box(front+Vector3.up*(eave+(level+.5f)*rise/8),new Vector3(width*(1-(level+.5f)/8),rise/8,.16f),"medieval_wood");
            }
            Box(c+new Vector3(width*.22f,eave+1.25f,depth*.24f),new Vector3(.72f,2.9f,.81f),"castle_brick_01");
        }

        // Joins timber posts with two horizontal rails outside the playable lane.
        private void Fence(float x,float start,float end)
        {
            for(var z=start;z<=end;z+=4) Box(new Vector3(x,.62f,z),new Vector3(.18f,1.24f,.18f),"medieval_wood",true);
            foreach(var height in new[]{.44f,.97f}) Box(new Vector3(x,height,(start+end)*.5f),new Vector3(.1f,.14f,end-start),"medieval_wood",true);
        }

        // Builds a timber A-frame with a layered practice target facing the firing lane.
        private void Target(float x,float z)
        {
            var c=new Vector3(x,0,z);
            var side=Mathf.Sign(z);
            Beam(c+new Vector3(-.55f,0,.05f),c+new Vector3(0,1.66f,0),.12f);
            Beam(c+new Vector3(.55f,0,.05f),c+new Vector3(0,1.66f,0),.12f);
            Beam(c+new Vector3(0,0,side*.7f),c+Vector3.up*1.56f,.13f);
            var disc=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            var mesh=disc.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(disc);
            var face=c+new Vector3(0,1.2f,0);
            for(var layer=0;layer<3;layer++)
            {
                var diameter=1.2f-layer*.38f;
                Add(mesh,Matrix4x4.TRS(face+new Vector3(0,0,-side*layer*.075f),Quaternion.Euler(90,0,0),
                    new Vector3(diameter,.13f-layer*.04f,diameter)),layer==0?"straw":layer==1?"red cloth":"straw");
            }
        }
    }
}
