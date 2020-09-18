using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using UModLoader;
using System.IO;
using System.Globalization;
using Landfall.TABS;
using System.Net;

namespace WebTabs
{
    public static class WebUtils
    {
        public static Dictionary<string, Dictionary<string, float>> scaleDictionaries = new Dictionary<string, Dictionary<string, float>>();

        public static Dictionary<string, Tuple<Tuple<Material, Material>, Tuple<Material, Material>>> eyeDictionary = new Dictionary<string, Tuple<Tuple<Material, Material>, Tuple<Material, Material>>>();
        public static string DeepString(this GameObject self)
        {
            string final = "\nGameObject '" + self.name + "':\n{\n\tComponents:\n\t{\n";
            final += String.Concat(from Component component in self.GetComponents<Component>() select ("\t\t" + component.GetType().Name + "\n"));
            final += "\t}\n";
            if (self.transform.childCount > 0)
            {
                final += "\tChildren:\n\t{\n";
                final += String.Concat(from Transform child in self.transform select (child.gameObject.DeepString().Replace("\n", "\n\t\t")));
                final += "\n\t}\n";
            }
            final += "}\n";
            return final;
        }
        private static void GetAllChildren(Transform parent, Dictionary<string, Transform> destination)
        {
            if (parent.childCount > 0)
            {
                foreach (Transform child in parent)
                {
                    destination[child.gameObject.name] = child;
                    GetAllChildren(child, destination);
                }
            }
        }

        public static void SetMeshRenderers(GameObject game_object, bool enabled)
        {
            Renderer[] renderers = game_object.GetComponentsInChildren<Renderer>();
            Type mrType = typeof(MeshRenderer);
            Type srType = typeof(SkinnedMeshRenderer);
            for (int i = 0; i < renderers.Length; i++)
            {
                Type rendererType = renderers[i].GetType();
                if(rendererType == mrType || rendererType == srType)
                {
                    renderers[i].enabled = enabled;
                }
            }
        }

        private static int[] GenerateRange(int floor, int ceiling)
        {
            int[] range = new int[ceiling - floor];
            for (int i = 0; i < range.Length; i++) range[i] = floor + i;
            return range.ToArray();
        }

        private static object GetVDicItem(string category, string key)
        {
            object item = null;
            if(WebTabsSettings.legacyConversion.ContainsKey(key)) key = WebTabsSettings.legacyConversion[key];
            if(ULoader.VDic.ContainsKey(category) && ULoader.VDic[category].ContainsKey(key)) item = ULoader.VDic[category][key];
            else 
            {
                if(!ULoader.VDic.ContainsKey(category) && WebTabsSettings.propStandIn) item = ULoader.VDic[category][ULoader.VDic[category].Keys.First()];
                if(WebTabsSettings.doStepTest) Debug.Log("[WEBTABS] Couldn't find dictionary item under '"+category+"' with key of '"+key+"'.");
            }
            return item;
        }

        private static (Mesh[] meshes, Material[][] materials) MakeWebMeshes(string data)
        {
            string[] models = data.Slice("<msep>");
            Mesh[] meshes = new Mesh[models.Length];
            Material[][] materials = new Material[models.Length][];
            for(int oI = 0; oI < models.Length; oI++)
            {
                string[] rawVerts = GetElementByTagName(models[oI], "vertexes").Slice("<vec>");
                string[] rawNorms = GetElementByTagName(models[oI], "normals").Slice("<nrm>");
                string[] rawTris = GetElementByTagName(models[oI], "triangles").Slice("<sep>");
                string[] rawMats = GetElementByTagName(models[oI], "materials").Slice("<sep>");

                Vector3[] vertexes = new Vector3[rawVerts.Length];
                Vector3[] normals = new Vector3[rawNorms.Length];
                int[][] subMeshRegions = new int[rawTris.Length][];
                Material[] thisMaterials = new Material[rawMats.Length];

                for(int i = 0; i < rawVerts.Length; i++)
                {
                    string[] cSplit = rawVerts[i].Slice("<sep>");
                    vertexes[i] = new Vector3(ParseFloat(cSplit[0]), ParseFloat(cSplit[1]), ParseFloat(cSplit[2]));
                }

                for(int i = 0; i < rawNorms.Length; i++)
                {
                    string[] cSplit = rawNorms[i].Slice("<sep>");
                    normals[i] = new Vector3(ParseFloat(cSplit[0]), ParseFloat(cSplit[1]), ParseFloat(cSplit[2]));
                }

                int numberCache = 0;
                for (int i = 0; i < rawTris.Length; i++)
                {
                    int range = (int)ParseFloat(rawTris[i]);
                    subMeshRegions[i] = GenerateRange(numberCache, numberCache + range);
                    numberCache += range;
                }

                Mesh mesh = new Mesh();
                mesh.Clear();
                mesh.vertices = vertexes;
                mesh.normals = normals;
                mesh.subMeshCount = subMeshRegions.Length;
                for(int i = 0; i < subMeshRegions.Length; i++) mesh.SetTriangles(subMeshRegions[i], i);
                meshes[oI] = mesh;
                

                for(int i = 0; i < rawMats.Length; i++)
                {
                    Material mat = new Material(Shader.Find("Standard"));
                    mat.color = HexColor(rawMats[i]);
                    mat.SetFloat("_Glossiness", 0f);
                    thisMaterials[i] = mat;
                }
                materials[oI] = thisMaterials;
            }
            return (meshes, materials);
        }
        public static Dictionary<string, Transform> AllChildren(this Transform root)
        {
            var output = new Dictionary<string, Transform>();
            GetAllChildren(root, output);
            return output;
        }

        public static Dictionary<string, string> bodyPartNames = new Dictionary<string, string>
        {
            { "Head", "head" },
            { "Torso", "torso" },
            { "Hip", "hip" },
            { "Arm_Left", "leftArm" },
            { "Arm_Right", "rightArm" },
            { "Hand_Left", "leftHand" },
            { "Hand_Right", "rightHand" },
            { "Leg_Left", "legLeft" },
            { "Leg_Right", "legRight" },
            { "Foot_Left", "footLeft" },
            { "Foot_Right", "footRight" }
        };

        public static float ParseFloat(string data)
        {
            float outF = 1f;
            try
            {
                outF = float.Parse(data, CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                Debug.Log("[FLOATPARSE] Unable to parse string '" + data + "' as float");
            }
            return outF;
        }

        public static bool IsLod(this Renderer self)
        {
            LODGroup lodGroup = self.transform.root.GetComponentInChildren<LODGroup>();
            if(lodGroup)
            {
                LOD[] lods = lodGroup.GetLODs();
                foreach(LOD lod in lods)
                {
                    foreach(Renderer renderer in lod.renderers)
                    {
                        if(renderer == self) return true;
                    }
                }
            }
            return false;
        }

        public static Renderer GetBestRenderer(GameObject gameObject, Type rendererType = null)
        {
            if(rendererType == null) rendererType = typeof(Renderer);
            else if (!rendererType.IsSubclassOf(typeof(Renderer))) return null;
            Renderer renderer = null;
            Renderer[] candidates = gameObject.GetComponentsInChildren<Renderer>();
            bool superLocked = false;
            foreach (Renderer candidate in candidates)
            {
                if(candidate.GetType() == rendererType || rendererType == typeof(Renderer))
                {
                    string name = candidate.gameObject.name;
                    if (name.Contains("LOD0"))
                    {
                        renderer = candidate;
                        superLocked = true;
                    }
                    else if (name.Contains("CP_") && !superLocked)
                    {
                        renderer = candidate;
                        superLocked = true;
                    }
                    else if (name.ToLower().Contains(gameObject.gameObject.name.ToLower()) && !superLocked) renderer = candidate;
                    else if (!renderer && (name != "RightHand" && name != "LeftHand") && !superLocked) renderer = candidate;
                }
            }
            return renderer;
        }

        public static Renderer[] GetBestRenderers(GameObject gameObject, Type rendererType = null)
        {
            if(rendererType == null) rendererType = typeof(Renderer);
            else if (!rendererType.IsSubclassOf(typeof(Renderer))) return null;
            Renderer[] candidates = gameObject.GetComponentsInChildren<Renderer>();
            List<Renderer> chosen = new List<Renderer>();
            bool superLocked = false;
            LODGroup lodGroup = gameObject.GetComponentInChildren<LODGroup>();
            if(lodGroup)
            {
                superLocked = true;
                foreach(LOD lod in lodGroup.GetLODs())
                {
                    foreach(Renderer renderer in lod.renderers) chosen.Add(renderer);
                }
            }
            foreach (Renderer candidate in candidates)
            {
                if(!chosen.Contains(candidate) && (candidate.GetType() == rendererType || rendererType == typeof(Renderer)))
                {
                    string wName = gameObject.name;
                    if (wName.Contains("LOD") && candidate.transform.parent.name.ToLower().Contains("base"))
                    {
                        chosen.Add(candidate);
                        superLocked = true;
                    }
                    else if (wName.Contains("LOD"))
                    {
                        chosen.Add(candidate);
                        superLocked = true;
                    }
                    else if (wName.Contains("CP_") && !superLocked)
                    {
                        chosen.Add(candidate);
                        superLocked = true;
                    }
                    else if (wName.ToLower().Contains(gameObject.name.ToLower()) && !superLocked) chosen.Add(candidate);
                    else if (chosen.Count <= 0 && (wName != "RightHand" && wName != "LeftHand") && !superLocked) chosen.Add(candidate);
                }
            }
            return chosen.ToArray();
        }

        public static void ScaleBodyPart(this Unit self, string boneName, float scalar)
        {
            Component bodyPart = ((Component)self.data.GetField(bodyPartNames[boneName]));
            if(bodyPart) bodyPart.gameObject.transform.localScale *= scalar;
        }

        public static string[] Slice(this string self, string separator)
        {
            return self.Split(new string[] { separator }, StringSplitOptions.None);
        }

        public static string GetElementByTagName(string data, string tag)
        {
            string result = "None";
            if(data != null)
            {
                string[] array = ("W"+data+"W").Slice("<" + tag + ">");
                if (array.Length >= 3) result = array[1];
            }
            return result;
        }

        public static Color HexColor(string hexstring)
        {
            Color outC = new Color();
            ColorUtility.TryParseHtmlString("#" + hexstring, out outC);
            return outC;
        }

        private static void SetWeapon(this UnitBlueprint self, string weaponType, GameObject weapon)
        {
            if(weaponType == "right") self.RightWeapon = weapon;
            else if (weaponType == "left") self.LeftWeapon = weapon;
        }

        private static GameObject GetWeapon(this UnitBlueprint self, string weaponType)
        {
            GameObject weapon = null;
            if(weaponType == "right") weapon = self.RightWeapon;
            else if (weaponType == "left") weapon = self.LeftWeapon;
            return weapon;
        }
        public static void PrintDataFiles()
        {
            string wDir = "WebTabsPrints";
            if (!Directory.Exists(wDir)) Directory.CreateDirectory(wDir);
            string weapons = "";
            string rangeWeapons = "";
            var keys = ULoader.VDic["weapons"].Keys.ToArray();
            for (int i = 0; i < keys.Length; i++)
            {
                string colors = "";
                GameObject weapon = (GameObject)ULoader.VDic["weapons"][keys[i]];
                RangeWeapon rw = weapon.GetComponentInChildren<RangeWeapon>();
                GameObject projectile = null;
                if (rw) projectile = rw.ObjectToSpawn;
                MeshRenderer renderer = null;
                TeamColor teamColor = null;

                MeshRenderer[] candidates = weapon.GetComponentsInChildren<MeshRenderer>();
                bool superLocked = false;
                bool winchester = false;
                foreach (MeshRenderer candidate in candidates)
                {
                    string name = candidate.gameObject.name;
                    if (name.Contains("LOD0") && candidate.transform.parent.name.ToLower().Contains("base"))
                    {
                        renderer = candidate;
                        superLocked = true;
                        winchester = true;
                    }
                    else if (name.Contains("LOD0") && !winchester)
                    {
                        renderer = candidate;
                        superLocked = true;
                    }
                    else if (name.Contains("CP_") && !superLocked)
                    {
                        renderer = candidate;
                        superLocked = true;
                    }
                    else if (name.ToLower().Contains(weapon.gameObject.name.ToLower()) && !superLocked) renderer = candidate;
                    else if (!renderer && (name != "RightHand" && name != "LeftHand") && !superLocked) renderer = candidate;
                }
                if (renderer)
                {
                    teamColor = renderer.GetComponent<TeamColor>();
                    Material[] mats = renderer.materials;
                    for (int j = 0; j < mats.Length; j++)
                    {
                        colors += ColorUtility.ToHtmlStringRGB(mats[j].color);
                        if (j < (mats.Length - 1)) colors += "<col>";
                    }
                }

                superLocked = false;
                winchester = false;
                if (projectile)
                {
                    rangeWeapons += "<type>" + keys[i] + "<type><sep>"+Environment.NewLine;
                }


                weapons += "<type>" + keys[i] + "<type><color>" + colors + "<color><ranged>" + (weapon.GetComponentInChildren<RangeWeapon>() != null) + "<ranged>";
                if (i < (keys.Length - 1)) weapons += "<sep>" + Environment.NewLine;
            }
            File.WriteAllText(@"WebTabsPrints\Weapons.txt", weapons);
            File.WriteAllText(@"WebTabsPrints\Projectiles.txt", rangeWeapons.Substring(0, rangeWeapons.Length-(("<sep>" + Environment.NewLine).Length)));
            string clothes = "";

            var keys2 = ULoader.VDic["clothes"].Keys.ToArray();
            for (int i = 0; i < keys2.Length; i++)
            {
                string colors = "";
                GameObject cloth = (GameObject)ULoader.VDic["clothes"][keys2[i]];
                Renderer renderer = null;
                TeamColor teamColor = null;

                Renderer[] candidates = cloth.GetComponentsInChildren<Renderer>();
                bool superLocked = false;
                bool winchester = false;
                foreach (Renderer candidate in candidates)
                {
                    if(candidate.GetType() == typeof(MeshRenderer) || candidate.GetType() == typeof(SkinnedMeshRenderer))
                    {
                        string name = candidate.gameObject.name;
                        if (name.Contains("LOD0") && candidate.transform.parent.name.ToLower().Contains("base"))
                        {
                            renderer = candidate;
                            superLocked = true;
                            winchester = true;
                        }
                        else if (name.Contains("LOD0") && !winchester)
                        {
                            renderer = candidate;
                            superLocked = true;
                        }
                        else if (name.Contains("CP_") && !superLocked)
                        {
                            renderer = candidate;
                            superLocked = true;
                        }
                        else if (name.ToLower().Contains(cloth.gameObject.name.ToLower()) && !superLocked) renderer = candidate;
                        else if (!renderer && (name != "RightHand" && name != "LeftHand") && !superLocked) renderer = candidate;
                    }
                }
                if (renderer)
                {
                    teamColor = renderer.GetComponent<TeamColor>();
                    Material[] mats = renderer.materials;
                    for (int j = 0; j < mats.Length; j++)
                    {
                        colors += ColorUtility.ToHtmlStringRGB(mats[j].color);
                        if (j < (mats.Length - 1)) colors += "<col>";
                    }
                }
                clothes += "<type>" + keys2[i] + "<type><color>" + colors + "<color>";
                if (i < (keys2.Length - 1)) clothes += "<sep>" + Environment.NewLine;
            }
            File.WriteAllText(@"WebTabsPrints\Clothes.txt", clothes);

            string sprites = "";
            var keys3 = ULoader.VDic["sprites"].Keys.ToArray();
            for(int i = 1; i < keys3.Length; i++)
            {
                sprites += "<type>" + keys3[i] + "<type>";
                if (i < (keys3.Length - 1)) sprites += "<sep>" + Environment.NewLine;
            }
            File.WriteAllText(@"WebTabsPrints\Sprites.txt", sprites);

            string bases = "";
            var keys4 = ULoader.VDic["bases"].Keys.ToArray();
            for (int i = 0; i < keys4.Length; i++)
            {
                int mounts = (ULoader.VDic["bases"][keys4[i]] as GameObject).GetComponentsInChildren<MountPos>().Length;
                bases += "<type>" + keys4[i] + "<type><mounts>" + mounts + "<mounts>";
                if (i < (keys4.Length - 1)) bases += "<sep>" + Environment.NewLine;
            }
            File.WriteAllText(@"WebTabsPrints\Bases.txt", bases);

            string blueprints = "";
            var keys5 = ULoader.VDic["blueprints"].Keys.ToArray();
            for (int i = 0; i < keys5.Length; i++)
            {
                blueprints += "<type>" + keys5[i] + "<type>";
                if (i < (keys5.Length - 1)) blueprints += "<sep>" + Environment.NewLine;
            }
            File.WriteAllText(@"WebTabsPrints\Blueprints.txt", blueprints);


            string moves = "";
            var keys6 = ULoader.VDic["moves"].Keys.ToArray();
            for (int i = 0; i < keys6.Length; i++)
            {
                moves += "<type>" + keys6[i] + "<type>";
                if (i < (keys6.Length - 1)) moves += "<sep>" + Environment.NewLine;
            }
            File.WriteAllText(@"WebTabsPrints\Moves.txt", moves);

            string effects = "";
            var keys7 = ULoader.VDic["effects"].Keys.ToArray();
            for (int i = 0; i < keys7.Length; i++)
            {
                effects += "<type>" + keys7[i] + "<type>";
                if (i < (keys7.Length - 1)) effects += "<sep>" + Environment.NewLine;
            }
            File.WriteAllText(@"WebTabsPrints\Effects.txt", effects);

            string explosions = "";
            var keys8 = ULoader.VDic["explosions"].Keys.ToArray();
            for (int i = 0; i < keys8.Length; i++)
            {
                Explosion exp = ((GameObject)ULoader.VDic["explosions"][keys8[i]]).GetComponentInChildren<Explosion>();
                string matColors = "";
                if(exp)
                {
                    var eOBJ = exp.gameObject;
                    try
                    {
                        ParticleSystemRenderer[] pSys = eOBJ.GetComponentsInChildren<ParticleSystemRenderer>();
                        matColors = string.Join("<col>", (from ParticleSystemRenderer sys in pSys where (sys.transform.parent == eOBJ.transform || sys.transform == eOBJ.transform) select (sys.material.HasProperty("_Color") ? ColorUtility.ToHtmlStringRGB(sys.material.color) : "FFFFFF")));
                    }
                    catch(Exception)
                    {
                        matColors = "";
                    }
                }

                if(keys8[i] == "Leg_DarkP_Hands") matColors = "";
                explosions += "<type>" + keys8[i] + "<type><color>" +matColors+"<color>";
                if (i < (keys8.Length - 1)) explosions += "<sep>" + Environment.NewLine;
            }
            File.WriteAllText(@"WebTabsPrints\Explosions.txt", explosions);
        }

        public static (string key, UnitBlueprint blueprint)[] DeserializeUnit(string unitID, string unitData, Dictionary<string, UnitBlueprint> blueprintDictionary, Dictionary<string, string> riderDataDictionary = null, bool doStepTest = false, List<(string key, UnitBlueprint blueprint)> returnList = null)
        {
            try
            {
                if(doStepTest) Debug.Log("[STEPTEST] start");
                (Mesh[] meshes, Material[][] materials) customAssets = (null, null);
                if(!unitData.Contains("<nmd>") && unitData.Contains("<model>"))
                {
                    string[] dSplit = unitData.Slice("<ums>");
                    unitData = dSplit[0];
                    customAssets = MakeWebMeshes(dSplit[1]);
                }
                if(returnList == null) returnList = new List<(string key, UnitBlueprint blueprint)>();
                string name = GetElementByTagName(unitData, "name");
                string unitBase = GetElementByTagName(unitData, "unitbase");
                string unitVoice = GetElementByTagName(unitData, "voice");
                string unitDeath = GetElementByTagName(unitData, "death");
                string rightWeapon = GetElementByTagName(unitData, "rightweapon");
                string leftWeapon = GetElementByTagName(unitData, "leftweapon");
                string twoHands = GetElementByTagName(unitData, "twohands");
                string[] clothing = GetElementByTagName(unitData, "clothing").Slice("<sep>");
                string[] combatMove = GetElementByTagName(unitData, "combatmove").Slice("<sep>");
                string sprite = GetElementByTagName(unitData, "sprite");
                string unitHealth = GetElementByTagName(unitData, "health");
                string unitCost = GetElementByTagName(unitData, "cost");
                string unitSpeed = GetElementByTagName(unitData, "speed");
                string unitTurn = GetElementByTagName(unitData, "turn");
                string unitSize = GetElementByTagName(unitData, "size");
                string unitMass = GetElementByTagName(unitData, "mass");
                string unitBalance = GetElementByTagName(unitData, "balance");
                string unitForce = GetElementByTagName(unitData, "force");
                string customBody = GetElementByTagName(unitData, "custombody");
                string riders = GetElementByTagName(unitData, "riders");

                UnitBlueprint unit = UFunctions.CreateUnit(name, null, null);
                GameObject customBase = null;
                if (customBody == "true")
                {
                    customBase = WebTabsSettings.myPool.AddObject("basefor" + unitID, ((GameObject)GetVDicItem("bases", unitBase)));
                    customBase.AddComponent<WebTabsID>().id = unitID;
                    unit.UnitBase = customBase;
                    string skinColor = GetElementByTagName(unitData, "skin");
                    string skinColorTeam = GetElementByTagName(unitData, "team");
                    string rEyeColor = GetElementByTagName(unitData, "reye");
                    string lEyeColor = GetElementByTagName(unitData, "leye");
                    Tuple<Material, Material> rEyeMats = null;
                    Tuple<Material, Material> lEyeMats = null;
                    Tuple<Tuple<Material, Material>, Tuple<Material, Material>> eyeMats = null;

                    if (skinColor != "None")
                    {
                        string[] sep = skinColor.Slice("<sep>");
                        Material red = new Material(Shader.Find("Standard"));
                        red.color = HexColor(sep[0]);
                        Color emissionR = red.color * ParseFloat(sep[2]);
                        red.SetColor("_EmissionColor", emissionR);
                        red.EnableKeyword("_EMISSION");
                        red.SetFloat("_Glossiness", 0f);

                        Material blue = new Material(Shader.Find("Standard"));
                        blue.color = HexColor(sep[1]);
                        Color emissionB = blue.color * ParseFloat(sep[3]);
                        blue.SetColor("_EmissionColor", emissionB);
                        blue.EnableKeyword("_EMISSION");
                        blue.SetFloat("_Glossiness", 0f);

                        TeamColor[] allTC = customBase.GetComponentsInChildren<TeamColor>();
                        foreach (TeamColor tc in allTC)
                        {
                            tc.redMaterial = red;
                            tc.blueMaterial = blue;
                        }
                    }

                    if(skinColorTeam != "None")
                    {
                        string[] sep = skinColorTeam.Slice("<sep>");
                        Material body = new Material(Shader.Find("Standard"));
                        body.color = HexColor(sep[0]);

                        Color emissionB = body.color * ParseFloat(sep[1]);
                        body.EnableKeyword("_EMISSION");
                        body.SetFloat("_Glossiness", 0f);

                        Renderer[] allRenderers = customBase.GetComponentsInChildren<Renderer>();
                        allRenderers = (from Renderer renderer in allRenderers where (renderer.GetType() == typeof(MeshRenderer) || renderer.GetType() == typeof(SkinnedMeshRenderer)) select renderer).ToArray();
                        
                        foreach(Renderer renderer in allRenderers)
                        {
                            TeamColor tc = renderer.GetComponent<TeamColor>();
                            Material[] newMats = new Material[renderer.materials.Length];
                            for(int i = 0; i < renderer.materials.Length; i++)
                            {
                                if(!tc || i != tc.materialID) newMats[i] = body;
                                else newMats[i] = renderer.materials[i];
                            }
                            renderer.materials = newMats;
                        }
                    }

                    if (rEyeColor != "None")
                    {
                        string[] sep = rEyeColor.Slice("<sep>");
                        Material iris = new Material(Shader.Find("Standard"));
                        iris.color = HexColor(sep[0]);
                        Color emissionI = iris.color * ParseFloat(sep[2]);
                        iris.SetColor("_EmissionColor", emissionI);
                        iris.EnableKeyword("_EMISSION");
                        iris.SetFloat("_Glossiness", 0f);

                        Material pupil = new Material(Shader.Find("Standard"));
                        pupil.color = HexColor(sep[1]);
                        Color emissionB = pupil.color * ParseFloat(sep[3]);
                        pupil.SetColor("_EmissionColor", emissionB);
                        pupil.EnableKeyword("_EMISSION");
                        pupil.SetFloat("_Glossiness", 0f);

                        rEyeMats = new Tuple<Material, Material>(iris, pupil);
                    }

                    if (lEyeColor != "None")
                    {
                        string[] sep = lEyeColor.Slice("<sep>");
                        Material iris = new Material(Shader.Find("Standard"));
                        iris.color = HexColor(sep[0]);
                        Color emissionI = iris.color * ParseFloat(sep[2]);
                        iris.SetColor("_EmissionColor", emissionI);
                        iris.EnableKeyword("_EMISSION");
                        iris.SetFloat("_Glossiness", 0f);

                        Material pupil = new Material(Shader.Find("Standard"));
                        pupil.color = HexColor(sep[1]);
                        Color emissionB = pupil.color * ParseFloat(sep[3]);
                        pupil.SetColor("_EmissionColor", emissionB);
                        pupil.EnableKeyword("_EMISSION");
                        pupil.SetFloat("_Glossiness", 0f);

                        lEyeMats = new Tuple<Material, Material>(iris, pupil);
                    }

                    if (rEyeMats != null && lEyeMats != null) eyeMats = new Tuple<Tuple<Material, Material>, Tuple<Material, Material>>(rEyeMats, lEyeMats);
                    else if (rEyeMats != null) eyeMats = new Tuple<Tuple<Material, Material>, Tuple<Material, Material>>(rEyeMats, null);
                    else if (lEyeMats != null) eyeMats = new Tuple<Tuple<Material, Material>, Tuple<Material, Material>>(null, lEyeMats);


                    var boneDict = new Dictionary<string, float>();
                    boneDict["Head"] = ParseFloat(GetElementByTagName(unitData, "head"));
                    boneDict["Torso"] = ParseFloat(GetElementByTagName(unitData, "torso"));
                    boneDict["Hip"] = ParseFloat(GetElementByTagName(unitData, "hip"));
                    boneDict["Arm_Left"] = ParseFloat(GetElementByTagName(unitData, "larm"));
                    boneDict["Arm_Right"] = ParseFloat(GetElementByTagName(unitData, "rarm"));
                    boneDict["Hand_Left"] = ParseFloat(GetElementByTagName(unitData, "lhand"));
                    boneDict["Hand_Right"] = ParseFloat(GetElementByTagName(unitData, "rhand"));
                    boneDict["Leg_Left"] = ParseFloat(GetElementByTagName(unitData, "lleg"));
                    boneDict["Leg_Right"] = ParseFloat(GetElementByTagName(unitData, "rleg"));
                    boneDict["Foot_Left"] = ParseFloat(GetElementByTagName(unitData, "lfoot"));
                    boneDict["Foot_Right"] = ParseFloat(GetElementByTagName(unitData, "rfoot"));
                    if (doStepTest) Debug.Log("[STEPTEST] bones");
                    scaleDictionaries[unitID] = boneDict;
                    eyeDictionary[unitID] = eyeMats;
                    if (doStepTest) Debug.Log("[STEPTEST] dictionaries");
                    customBase.transform.SetHideFlagsChildren();
                }
                else unit.UnitBase = (GameObject)GetVDicItem("bases", unitBase);
                if (doStepTest) Debug.Log("[STEPTEST] body");
                unit.fistRef = ((UnitBlueprint)ULoader.VDic["blueprints"]["Halfling"]).fistRef;
                if (unitVoice != "None") unit.vocalRef = ((UnitBlueprint)GetVDicItem("blueprints", unitVoice)).vocalRef;
                if (unitDeath != "None") unit.deathRef = ((UnitBlueprint)GetVDicItem("blueprints", unitDeath)).deathRef;
                if (doStepTest) Debug.Log("[STEPTEST] vocal");

                string weaponData = rightWeapon;
                string weaponSide = "right";

                for(int weaponIndex = 0; weaponIndex < 2; weaponIndex++)
                {
                    if(weaponIndex == 1)
                    {
                        weaponData = leftWeapon;
                        weaponSide = "left";
                    }
                    if (weaponData != "None")
                    {
                        string type = GetElementByTagName(weaponData, "type");
                        string custom = GetElementByTagName(weaponData, "custom");

                        if (type != "None")
                        {
                            if (custom != "true")
                            {
                                unit.SetWeapon(weaponSide, (GameObject)GetVDicItem("weapons", type));
                            }
                            else
                            {
                                string damage = GetElementByTagName(weaponData, "damage");
                                string speed = GetElementByTagName(weaponData, "speed");
                                string range = GetElementByTagName(weaponData, "range");
                                string force = GetElementByTagName(weaponData, "force");
                                string impact = GetElementByTagName(weaponData, "impact");
                                string impactColors = GetElementByTagName(weaponData, "impactcolors");
                                string teammates = GetElementByTagName(weaponData, "teammates");
                                string effect = GetElementByTagName(weaponData, "effect");
                                string projectile = GetElementByTagName(weaponData, "projectile");
                                string[] scale = GetElementByTagName(weaponData, "scale").Slice("<sep>");
                                string materials = GetElementByTagName(weaponData, "materials");
                                string model = GetElementByTagName(weaponData, "model");

                                if(effect == "null") effect = "None";
                                GameObject customWeapon = WebTabsSettings.myPool.AddObject(weaponSide + "Weapon" + unitID, (GameObject)GetVDicItem("weapons", type));
                                unit.SetWeapon(weaponSide, customWeapon);
                                Weapon weapon = customWeapon.GetComponentInChildren<Weapon>();
                                if (weapon)
                                {
                                    if (range != "None") weapon.maxRange *= ParseFloat(range);
                                    if (speed != "None") weapon.internalCooldown *= ParseFloat(speed);
                                }
                                customWeapon.transform.localScale = new Vector3(ParseFloat(scale[0]), ParseFloat(scale[1]), ParseFloat(scale[2]));

                                if(model != "None" && customAssets != (null, null))
                                {
                                    int modelIndex = (int)ParseFloat(GetElementByTagName(model, "index"));
                                    string[] modelOffset = GetElementByTagName(model, "offset").Slice("<sep>");     
                                    string[] modelScale = GetElementByTagName(model, "scale").Slice("<sep>");    
                                    string[] modelRotation = GetElementByTagName(model, "rotation").Slice("<sep>");   
                                    string[] modelMaterials = GetElementByTagName(model, "materials").Slice("<mat>");

                                    SetMeshRenderers(customWeapon, false);
                                    GameObject PinaContainer = new GameObject();
                                    MeshFilter meshFilter = PinaContainer.AddComponent<MeshFilter>();
                                    MeshRenderer meshRenderer = PinaContainer.AddComponent<MeshRenderer>();
                                    meshFilter.mesh = customAssets.meshes[modelIndex];
                                    Material[] defaultMats = customAssets.materials[modelIndex];
                                    Material[] newMaterials = new Material[defaultMats.Length];
                                    for(int i = 0; i < defaultMats.Length; i++)
                                    {
                                        Material chosen = null;
           
                                        if(modelMaterials[i] != "None")
                                        {
                                            string[] fields = modelMaterials[i].Slice("<col>");
                                            Material mat = new Material(Shader.Find("Standard"));
                                            mat.color = HexColor(fields[0]);
                                            Color emission = mat.color * ParseFloat(fields[1]);
                                            mat.SetColor("_EmissionColor", emission);
                                            mat.EnableKeyword("_EMISSION");
                                            mat.SetFloat("_Glossiness", 0f);
                                            chosen = mat;
                                        }
                                        else chosen = defaultMats[i];
                                       newMaterials[i] = chosen;
                                    }
                                    meshRenderer.materials = newMaterials;
 
                                    PinaContainer.transform.position = customWeapon.transform.position;
                                    PinaContainer.transform.rotation = customWeapon.transform.rotation;
                                    PinaContainer.transform.localScale = new Vector3(ParseFloat(modelScale[0]), ParseFloat(modelScale[1]), ParseFloat(modelScale[2]));
                                    PinaContainer.transform.SetParent(customWeapon.transform);
                                    PinaContainer.transform.localPosition = new Vector3(ParseFloat(modelOffset[0]), ParseFloat(modelOffset[1]), ParseFloat(modelOffset[2]));
                                    PinaContainer.transform.localRotation = Quaternion.Euler(new Vector3(ParseFloat(modelRotation[0]), ParseFloat(modelRotation[1]), ParseFloat(modelRotation[2])));
                                    customWeapon.transform.SetHideFlagsChildren();
                                }
                                Material[] impactMaterials = null;
                                if(impact != "None" && impact != "null" && impactColors != "" && impactColors != "None")
                                {
                                    string[] mSplit = impactColors.Slice("<col>");
                                    impactMaterials = new Material[mSplit.Length];
                                    for(int i = 0; i < impactMaterials.Length; i++)
                                    {
                                        if(mSplit[i] != "None")
                                        {
                                            string[] fields = mSplit[i].Slice("<sep>");
                                            impactMaterials[i] = new Material(Shader.Find("Standard"));
                                            impactMaterials[i].color = HexColor(fields[0]);
                                            Color emission = impactMaterials[i].color * ParseFloat(fields[1]);
                                            impactMaterials[i].SetColor("_EmissionColor", emission);
                                            impactMaterials[i].EnableKeyword("_EMISSION");
                                            impactMaterials[i].SetFloat("_Glossiness", 0f);
                                        }
                                    }
                                }

                                RangeWeapon ranged = customWeapon.GetComponentInChildren<RangeWeapon>();
                                if (ranged)
                                {
                                    string projectileType = GetElementByTagName(projectile, "type");
                                    string projectileMaterial = GetElementByTagName(projectile, "material");
                                    string projectileNumber = GetElementByTagName(projectile, "number");
                                    string projectileScale = GetElementByTagName(projectile, "scale");
                                    string projectileBurst = GetElementByTagName(projectile, "burst");
                                    string projectileSpread = GetElementByTagName(projectile, "spread");
                                    string projectileAmmo = GetElementByTagName(projectile, "ammo");
                                    string projectileReload = GetElementByTagName(projectile, "reload");
                                    GameObject customProjectile = null;
                                    if(projectileType != null && projectileType != "None" && projectileType != "null")
                                    {
                                        customProjectile = WebTabsSettings.myPool.AddObject(weaponSide + "WeaponProj" + unitID, ((GameObject)GetVDicItem("weapons", projectileType)).GetComponentInChildren<RangeWeapon>().ObjectToSpawn);
                                        if(projectileScale != "None")
                                        {
                                            string[] split = projectileScale.Slice("<sep>");
                                            customProjectile.transform.localScale = new Vector3(ParseFloat(split[0]), ParseFloat(split[1]), ParseFloat(split[2]));
                                        }
                                        if(projectileMaterial != "None")
                                        {
                                            string[] pMatSplit = projectileMaterial.Slice("<sep>");
                                            Color pColor = HexColor(pMatSplit[0]);
                                            Color pEmission = pColor * ParseFloat(pMatSplit[1]);
                                            Material newMat = new Material(Shader.Find("Standard"));
                                            newMat.color = pColor;
                                            newMat.SetColor("_EmissionColor", pEmission);
                                            newMat.EnableKeyword("_EMISSION");
                                            newMat.SetFloat("_Glossiness", 0f);
                                            Renderer[] projRS = null;
                                            if(WebTabsSettings.particleWeapons.Contains(projectileType)) projRS = customProjectile.GetComponentsInChildren<Renderer>();
                                            else projRS = customProjectile.GetComponentsInChildren<MeshRenderer>();
                                            for(int i = 0; i < projRS.Length; i++)
                                            {
                                                Material[] allMats = projRS[i].materials;
                                                List<Material> newMats = new List<Material>();
                                                for(int j = 0; j < allMats.Length; j++) newMats.Add(newMat);
                                                projRS[i].materials = newMats.ToArray();
                                            }
                                        }
                                        customProjectile.transform.SetHideFlagsChildren();
                                    }
                                    else customProjectile = WebTabsSettings.myPool.AddObject(weaponSide + "WeaponProj" + unitID, ranged.ObjectToSpawn);

                                    ranged.ObjectToSpawn = customProjectile;
                                    if(projectileNumber != "" && projectileNumber != "None" && projectileNumber !="null") ranged.numberOfObjects = (int)ParseFloat(projectileNumber);
                                    if(projectileBurst != "None")
                                    {
                                        ranged.useRandomDelay = false;
                                        ranged.delayPerSpawn = ParseFloat(projectileBurst);
                                    }
                                    if(projectileSpread != "None") ranged.spread = ParseFloat(projectileSpread);
                                    if(projectileAmmo != "None" && projectileAmmo != "0" && projectileAmmo != "1")
                                    {
                                        AmmoSystem ammoSystem = ranged.gameObject.AddComponent<AmmoSystem>();
                                        ammoSystem.ammoMax = (int)ParseFloat(projectileAmmo);
                                        ammoSystem.ammoCount = ammoSystem.ammoMax;
                                        ammoSystem.ammoReloadtime = ParseFloat(projectileReload);
                                        ammoSystem.ammoCooldown = ParseFloat(speed);
                                        ranged.gameObject.transform.SetHideFlagsChildren();
                                    }
                                    ProjectileHit projHit = customProjectile.GetComponentInChildren<ProjectileHit>();
                                    TargetableEffect targetableEffect = customProjectile.GetComponentInChildren<TargetableEffect>();
                                    CollisionWeapon colWeapon = customProjectile.GetComponentInChildren<CollisionWeapon>();
                                    if (projHit)
                                    {
                                        if (damage != "None") projHit.damage *= ParseFloat(damage);
                                        if (force != "None") projHit.force *= ParseFloat(force);

                                        if (impact != null && impact != "None")
                                        {   
                                            Explosion explosion = ((GameObject)GetVDicItem("explosions", impact)).GetComponentInChildren<Explosion>();
                                            if(explosion)
                                            {
                                                GameObject explosionObject = WebTabsSettings.myPool.AddObject(weaponSide + "WeaponExplosion" + unitID, explosion.gameObject);
                                                foreach(Transform child in explosionObject.transform) 
                                                {
                                                    if(!child.GetComponent<ParticleSystem>()) GameObject.DestroyImmediate(child.gameObject);
                                                    else
                                                    {
                                                        var main = child.GetComponent<ParticleSystem>().main;
                                                        main.playOnAwake = true;
                                                        foreach(Component component in child.GetComponents<Component>())
                                                        {
                                                            Type componentType = component.GetType();
                                                            if(componentType != typeof(Explosion) && componentType != typeof(AddObjectEffect) && componentType != typeof(Transform) && componentType != typeof(RemoveAfterSeconds) && componentType != typeof(PlaySoundEffect) && componentType != typeof(ParticleSystem) && componentType != typeof(ParticleSystemRenderer)) GameObject.DestroyImmediate(component);
                                                        }
                                                    }
                                                }
                                                foreach(Component component in explosionObject.GetComponents<Component>())
                                                {
                                                    Type componentType = component.GetType();
                                                    if(componentType != typeof(Explosion) && componentType != typeof(AddObjectEffect) && componentType != typeof(Transform) && componentType != typeof(RemoveAfterSeconds) && componentType != typeof(PlaySoundEffect) && componentType != typeof(ParticleSystem) && componentType != typeof(ParticleSystemRenderer)) GameObject.DestroyImmediate(component);
                                                }
                                                explosion = explosionObject.GetComponent<Explosion>();
                                                explosion.ignoreTeamMates = (teammates == "true");
                                                explosion.SetField("inited", false);
                                                if(impactMaterials != null)
                                                {
                                                    ParticleSystemRenderer[] particleSystems = (from ParticleSystemRenderer system in explosionObject.GetComponentsInChildren<ParticleSystemRenderer>() where (system.material.HasProperty("_Color")) select system).ToArray();
                                                    for(int i = 0; i < particleSystems.Length; i++)
                                                    {
                                                        if(i < impactMaterials.Length && impactMaterials[i])
                                                        {
                                                            particleSystems[i].material = impactMaterials[i];
                                                            ParticleSystem system = particleSystems[i].GetComponent<ParticleSystem>();
                                                            if(system && system.trails.enabled) particleSystems[i].trailMaterial = impactMaterials[i];
                                                        }
                                                    }
                                                }
                                                projHit.objectsToSpawn = new ObjectToSpawn[] { new ObjectToSpawn { objectToSpawn = explosionObject } };
                                                if (effect != null && effect != "None")
                                                {
                                                    UnitEffectBase effectPrefab = ((GameObject)GetVDicItem("effects", effect)).GetComponentInChildren<UnitEffectBase>();
                                                    ProjectileHitAddEffect addEffect = customProjectile.GetComponentInChildren<ProjectileHitAddEffect>();
                                                    if (!addEffect) addEffect = customProjectile.AddComponent<ProjectileHitAddEffect>();
                                                    addEffect.EffectPrefab = effectPrefab;
                                                    
                                                    if (explosion)
                                                    {
                                                        AddObjectEffect AOE = explosionObject.GetComponent<AddObjectEffect>();
                                                        if (!AOE) AOE = explosionObject.AddComponent<AddObjectEffect>();
                                                        AOE.EffectPrefab = effectPrefab;
                                                    }
                                                }
                                            }
                                        }
                                        else if (effect != null && effect != "None")
                                        {
                                            UnitEffectBase effectPrefab = ((GameObject)GetVDicItem("effects", effect)).GetComponentInChildren<UnitEffectBase>();
                                            ProjectileHitAddEffect addEffect = customProjectile.GetComponentInChildren<ProjectileHitAddEffect>();
                                            if (!addEffect) addEffect = customProjectile.AddComponent<ProjectileHitAddEffect>();
                                            addEffect.EffectPrefab = effectPrefab;
                                        }
                                    }
                                    else if (targetableEffect)
                                    {
                                        DamageTargetableEffect damageTargetableEffect = customProjectile.GetComponentInChildren<DamageTargetableEffect>();
                                        if(!damageTargetableEffect) damageTargetableEffect = customProjectile.AddComponent<DamageTargetableEffect>();
                                        if (damage != "None") damageTargetableEffect.damage *= ParseFloat(damage);
                                        if (force != "None") damageTargetableEffect.force *= ParseFloat(force);

                                        SpawnTargetableEffect spawnTargetableEffect = customProjectile.GetComponentInChildren<SpawnTargetableEffect>();
                                        if(!spawnTargetableEffect) spawnTargetableEffect = customProjectile.AddComponent<SpawnTargetableEffect>();

                                        if (impact != null && impact != "None")
                                        {
                                            Explosion explosion = ((GameObject)GetVDicItem("explosions", impact)).GetComponentInChildren<Explosion>();
                                            if(explosion)
                                            {
                                                GameObject explosionObject = WebTabsSettings.myPool.AddObject(weaponSide + "WeaponExplosion" + unitID, explosion.gameObject);
                                                foreach(Transform child in explosionObject.transform) 
                                                {
                                                    if(!child.GetComponent<ParticleSystem>()) GameObject.DestroyImmediate(child.gameObject);
                                                    else
                                                    {
                                                        var main = child.GetComponent<ParticleSystem>().main;
                                                        main.playOnAwake = true;
                                                        foreach(Component component in child.GetComponents<Component>())
                                                        {
                                                            Type componentType = component.GetType();
                                                            if(componentType != typeof(Explosion) && componentType != typeof(AddObjectEffect) && componentType != typeof(Transform) && componentType != typeof(RemoveAfterSeconds) && componentType != typeof(PlaySoundEffect) && componentType != typeof(ParticleSystem) && componentType != typeof(ParticleSystemRenderer)) GameObject.DestroyImmediate(component);
                                                        }
                                                    }
                                                }
                                                foreach(Component component in explosionObject.GetComponents<Component>())
                                                {
                                                    Type componentType = component.GetType();
                                                    if(componentType != typeof(Explosion) && componentType != typeof(AddObjectEffect) && componentType != typeof(Transform) && componentType != typeof(RemoveAfterSeconds) && componentType != typeof(PlaySoundEffect) && componentType != typeof(ParticleSystem) && componentType != typeof(ParticleSystemRenderer)) GameObject.DestroyImmediate(component);
                                                }
                                                explosion = explosionObject.GetComponent<Explosion>();
                                                explosion.ignoreTeamMates = (teammates == "true");
                                                explosion.SetField("inited", false);
                                                if(impactMaterials != null)
                                                {
                                                    ParticleSystemRenderer[] particleSystems = (from ParticleSystemRenderer system in explosionObject.GetComponentsInChildren<ParticleSystemRenderer>() where (system.material.HasProperty("_Color")) select system).ToArray();
                                                    for(int i = 0; i < particleSystems.Length; i++)
                                                    {
                                                        if(i < impactMaterials.Length && impactMaterials[i])
                                                        {
                                                            particleSystems[i].material = impactMaterials[i];
                                                            ParticleSystem system = particleSystems[i].GetComponent<ParticleSystem>();
                                                            if(system && system.trails.enabled) particleSystems[i].trailMaterial = impactMaterials[i];
                                                        }
                                                    }
                                                }
                                                spawnTargetableEffect.objectToSpawn = explosionObject;
                                                if (effect != null && effect != "None")
                                                {
                                                    UnitEffectBase effectPrefab = ((GameObject)GetVDicItem("effects", effect)).GetComponentInChildren<UnitEffectBase>();
                                                    AddTargetableEffect addEffect = customProjectile.GetComponentInChildren<AddTargetableEffect>();
                                                    if (!addEffect) addEffect = customProjectile.AddComponent<AddTargetableEffect>();
                                                    addEffect.EffectPrefab = effectPrefab;

                                                    if (explosion)
                                                    {
                                                        AddObjectEffect AOE = explosionObject.GetComponent<AddObjectEffect>();
                                                        if (!AOE) AOE = explosionObject.AddComponent<AddObjectEffect>();
                                                        AOE.EffectPrefab = effectPrefab;
                                                    }
                                                }
                                            }
                                        }
                                        else if (effect != null && effect != "None")
                                        {
                                            UnitEffectBase effectPrefab = ((GameObject)GetVDicItem("effects", effect)).GetComponentInChildren<UnitEffectBase>();
                                            AddTargetableEffect addEffect = customProjectile.GetComponentInChildren<AddTargetableEffect>();
                                            if (!addEffect) addEffect = customProjectile.AddComponent<AddTargetableEffect>();
                                            addEffect.EffectPrefab = effectPrefab;
                                        }
                                    }
                                    else if(colWeapon)
                                    {
                                        if (damage != "None") colWeapon.damage *= ParseFloat(damage);
                                        if (force != "None") colWeapon.onImpactForce *= ParseFloat(force);

                                        if (impact != null && impact != "None")
                                        {
                                            MeleeWeaponSpawn meleeSpawn = colWeapon.GetComponent<MeleeWeaponSpawn>();
                                            if (!meleeSpawn)
                                            {
                                                meleeSpawn = colWeapon.gameObject.AddComponent<MeleeWeaponSpawn>();
                                                meleeSpawn.pos = MeleeWeaponSpawn.Pos.ContactPoint;
                                                meleeSpawn.rot = MeleeWeaponSpawn.Rot.Normal;
                                            }

                                            Explosion explosion = ((GameObject)GetVDicItem("explosions", impact)).GetComponentInChildren<Explosion>();
                                            if(explosion)
                                            {
                                                GameObject explosionObject = WebTabsSettings.myPool.AddObject(weaponSide + "WeaponExplosion" + unitID, explosion.gameObject);
                                                foreach(Transform child in explosionObject.transform) 
                                                {
                                                    if(!child.GetComponent<ParticleSystem>()) GameObject.DestroyImmediate(child.gameObject);
                                                    else
                                                    {
                                                        var main = child.GetComponent<ParticleSystem>().main;
                                                        main.playOnAwake = true;
                                                        foreach(Component component in child.GetComponents<Component>())
                                                        {
                                                            Type componentType = component.GetType();
                                                            if(componentType != typeof(Explosion) && componentType != typeof(AddObjectEffect) && componentType != typeof(Transform) && componentType != typeof(RemoveAfterSeconds) && componentType != typeof(PlaySoundEffect) && componentType != typeof(ParticleSystem) && componentType != typeof(ParticleSystemRenderer)) GameObject.DestroyImmediate(component);
                                                        }
                                                    }
                                                }
                                                foreach(Component component in explosionObject.GetComponents<Component>())
                                                {
                                                    Type componentType = component.GetType();
                                                    if(componentType != typeof(Explosion) && componentType != typeof(AddObjectEffect) && componentType != typeof(Transform) && componentType != typeof(RemoveAfterSeconds) && componentType != typeof(PlaySoundEffect) && componentType != typeof(ParticleSystem) && componentType != typeof(ParticleSystemRenderer)) GameObject.DestroyImmediate(component);
                                                }
                                                explosion = explosionObject.GetComponent<Explosion>();
                                                explosion.ignoreTeamMates = (teammates == "true");
                                                explosion.SetField("inited", false);
                                                if(impactMaterials != null)
                                                {
                                                    ParticleSystemRenderer[] particleSystems = (from ParticleSystemRenderer system in explosionObject.GetComponentsInChildren<ParticleSystemRenderer>() where (system.material.HasProperty("_Color")) select system).ToArray();
                                                    for(int i = 0; i < particleSystems.Length; i++)
                                                    {
                                                        if(i < impactMaterials.Length && impactMaterials[i])
                                                        {
                                                            particleSystems[i].material = impactMaterials[i];
                                                            ParticleSystem system = particleSystems[i].GetComponent<ParticleSystem>();
                                                            if(system && system.trails.enabled) particleSystems[i].trailMaterial = impactMaterials[i];
                                                        }
                                                    }
                                                }
                                                meleeSpawn.objectToSpawn = explosionObject;
                                                if (effect != null && effect != "None")
                                                {
                                                    UnitEffectBase effectPrefab = ((GameObject)GetVDicItem("effects", effect)).GetComponentInChildren<UnitEffectBase>();
                                                    MeleeWeaponAddEffect addEffect = colWeapon.GetComponentInChildren<MeleeWeaponAddEffect>();
                                                    if (!addEffect) addEffect = colWeapon.gameObject.AddComponent<MeleeWeaponAddEffect>();
                                                    addEffect.EffectPrefab = effectPrefab;

                                                    if (explosion)
                                                    {
                                                        AddObjectEffect AOE = explosionObject.GetComponent<AddObjectEffect>();
                                                        if (!AOE) AOE = explosionObject.AddComponent<AddObjectEffect>();
                                                        AOE.EffectPrefab = effectPrefab;
                                                    }
                                                }
                                            }
                                        }
                                        else if (effect != null && effect != "None")
                                        {
                                            UnitEffectBase effectPrefab = ((GameObject)GetVDicItem("effects", effect)).GetComponentInChildren<UnitEffectBase>();
                                            MeleeWeaponAddEffect addEffect = colWeapon.GetComponentInChildren<MeleeWeaponAddEffect>();
                                            if (!addEffect) addEffect = colWeapon.gameObject.AddComponent<MeleeWeaponAddEffect>();
                                            addEffect.EffectPrefab = effectPrefab;
                                        }
                                    }
                                }
                                else
                                {
                                    CollisionWeapon colWeapon = customWeapon.GetComponentInChildren<CollisionWeapon>();
                                    if (colWeapon)
                                    {
                                        if (damage != "None") colWeapon.damage *= ParseFloat(damage);
                                        if (force != "None") colWeapon.onImpactForce *= ParseFloat(force);

                                        if (impact != null && impact != "None")
                                        {
                                            MeleeWeaponSpawn meleeSpawn = colWeapon.GetComponent<MeleeWeaponSpawn>();
                                            if (!meleeSpawn)
                                            {
                                                meleeSpawn = colWeapon.gameObject.AddComponent<MeleeWeaponSpawn>();
                                                meleeSpawn.pos = MeleeWeaponSpawn.Pos.ContactPoint;
                                                meleeSpawn.rot = MeleeWeaponSpawn.Rot.Normal;
                                            }

                                            Explosion explosion = ((GameObject)GetVDicItem("explosions", impact)).GetComponentInChildren<Explosion>();
                                            if(explosion)
                                            {
                                                GameObject explosionObject = WebTabsSettings.myPool.AddObject(weaponSide + "WeaponExplosion" + unitID, explosion.gameObject);
                                                foreach(Transform child in explosionObject.transform) 
                                                {
                                                    if(!child.GetComponent<ParticleSystem>()) GameObject.DestroyImmediate(child.gameObject);
                                                    else
                                                    {
                                                        var main = child.GetComponent<ParticleSystem>().main;
                                                        main.playOnAwake = true;
                                                        foreach(Component component in child.GetComponents<Component>())
                                                        {
                                                            Type componentType = component.GetType();
                                                            if(componentType != typeof(Explosion) && componentType != typeof(AddObjectEffect) && componentType != typeof(Transform) && componentType != typeof(RemoveAfterSeconds) && componentType != typeof(PlaySoundEffect) && componentType != typeof(ParticleSystem) && componentType != typeof(ParticleSystemRenderer)) GameObject.DestroyImmediate(component);
                                                        }
                                                    }
                                                }
                                                foreach(Component component in explosionObject.GetComponents<Component>())
                                                {
                                                    Type componentType = component.GetType();
                                                    if(componentType != typeof(Explosion) && componentType != typeof(AddObjectEffect) && componentType != typeof(Transform) && componentType != typeof(RemoveAfterSeconds) && componentType != typeof(PlaySoundEffect) && componentType != typeof(ParticleSystem) && componentType != typeof(ParticleSystemRenderer)) GameObject.DestroyImmediate(component);
                                                }
                                                explosion = explosionObject.GetComponent<Explosion>();
                                                explosion.ignoreTeamMates = (teammates == "true");
                                                explosion.SetField("inited", false);
                                                if(impactMaterials != null)
                                                {
                                                    ParticleSystemRenderer[] particleSystems = (from ParticleSystemRenderer system in explosionObject.GetComponentsInChildren<ParticleSystemRenderer>() where (system.material.HasProperty("_Color")) select system).ToArray();
                                                    for(int i = 0; i < particleSystems.Length; i++)
                                                    {
                                                        if(i < impactMaterials.Length && impactMaterials[i])
                                                        {
                                                            particleSystems[i].material = impactMaterials[i];
                                                            ParticleSystem system = particleSystems[i].GetComponent<ParticleSystem>();
                                                            if(system && system.trails.enabled) particleSystems[i].trailMaterial = impactMaterials[i];
                                                        }
                                                    }
                                                }
                                                meleeSpawn.objectToSpawn = explosionObject;
                                                if (effect != null && effect != "None")
                                                {
                                                    UnitEffectBase effectPrefab = ((GameObject)GetVDicItem("effects", effect)).GetComponentInChildren<UnitEffectBase>();
                                                    MeleeWeaponAddEffect addEffect = colWeapon.GetComponentInChildren<MeleeWeaponAddEffect>();
                                                    if (!addEffect) addEffect = colWeapon.gameObject.AddComponent<MeleeWeaponAddEffect>();
                                                    addEffect.EffectPrefab = effectPrefab;

                                                    if (explosion)
                                                    {
                                                        AddObjectEffect AOE = explosionObject.GetComponent<AddObjectEffect>();
                                                        if (!AOE) AOE = explosionObject.AddComponent<AddObjectEffect>();
                                                        AOE.EffectPrefab = effectPrefab;
                                                    }
                                                }
                                            }
                                        }
                                        else if (effect != null && effect != "None")
                                        {
                                            UnitEffectBase effectPrefab = ((GameObject)GetVDicItem("effects", effect)).GetComponentInChildren<UnitEffectBase>();
                                            MeleeWeaponAddEffect addEffect = colWeapon.GetComponentInChildren<MeleeWeaponAddEffect>();
                                            if (!addEffect) addEffect = colWeapon.gameObject.AddComponent<MeleeWeaponAddEffect>();
                                            addEffect.EffectPrefab = effectPrefab;
                                        }
                                    }
                                }

                                if (materials != "None" && materials != "" && model == "None")
                                {
                                    Renderer[] renderers = GetBestRenderers(weapon.gameObject);
                                    Renderer bestRenderer = GetBestRenderer(weapon.gameObject);
                                    Dictionary<Color, Material> lodDictionary = new Dictionary<Color, Material>();
                                    Material[] rMaterials = null;
                                    string[] mats = materials.Slice("<col>");
                                    if(bestRenderer) rMaterials = bestRenderer.materials;
                                    else rMaterials = new Material[mats.Length];
                                    Material[] newMaterials = new Material[rMaterials.Length];
                                    
                                    for (int i = 0; i < newMaterials.Length; i++)
                                    {
                                        if (i < mats.Length && mats[i] != "None")
                                        {
                                            string[] fields = mats[i].Slice("<sep>");
                                            Material mat = new Material(Shader.Find("Standard"));
                                            mat.color = HexColor(fields[0]);
                                            Color emission = mat.color * ParseFloat(fields[1]);
                                            mat.SetColor("_EmissionColor", emission);
                                            mat.EnableKeyword("_EMISSION");
                                            mat.SetFloat("_Glossiness", 0f);
                                            newMaterials[i] = mat;
                                        }
                                        else newMaterials[i] = null;
                                    }
                                    if(bestRenderer)
                                    {
                                        for(int i = 0; i < rMaterials.Length; i++) lodDictionary[rMaterials[i].color] = newMaterials[i];
                                        if (renderers != null && renderers.Length > 0)
                                        {
                                            foreach (Renderer renderer in renderers)
                                            {
                                                List<TeamColor> teamColors = renderer.GetComponents<TeamColor>().ToList();
                                                List<TeamColor> removed = new List<TeamColor>();
                                                Material[] thisMaterials = renderer.materials;
                                                List<Material> dictMaterials = new List<Material>();
                                                for (int i = 0; i < thisMaterials.Length; i++)
                                                {
                                                    Material replacement = null;
                                                    if(lodDictionary.ContainsKey(thisMaterials[i].color)) replacement = lodDictionary[thisMaterials[i].color];
                                                    if (replacement != null)
                                                    {
                                                        dictMaterials.Add(replacement);
                                                        foreach(TeamColor tc in teamColors) 
                                                        {
                                                            if(!removed.Contains(tc) && tc.materialID == i)
                                                            {
                                                                removed.Add(tc);
                                                                UnityEngine.Object.DestroyImmediate(tc);
                                                            }
                                                        }
                                                    }
                                                    else dictMaterials.Add(thisMaterials[i]);
                                                }
                                                renderer.materials = dictMaterials.ToArray();
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (doStepTest) Debug.Log("[STEPTEST] weapons");

                int numCloths = 0;
                var clothingObjects = new List<GameObject>();
                foreach (string clothItem in clothing)
                {
                    numCloths++;
                    if (clothItem != "None" && clothItem != "")
                    {
                        string clothName = GetElementByTagName(clothItem, "type");
                        string materials = GetElementByTagName(clothItem, "materials");
                        string model = GetElementByTagName(clothItem, "model");
                        bool customC = (GetElementByTagName(clothItem, "custom") == "true");
                        if (!customC)
                        {
                            clothingObjects.Add((GameObject)GetVDicItem("clothes", clothName));
                        }
                        else
                        {
                            GameObject cloth = WebTabsSettings.myPool.AddObject("cloth" + numCloths + unitID, (GameObject)GetVDicItem("clothes", clothName));
                            clothingObjects.Add(cloth);

                            if (model != "None" && customAssets != (null, null))
                            {
                                model = model.Replace("<vc>", "<sep>");
                                int modelIndex = (int)ParseFloat(GetElementByTagName(model, "index"));
                                string[] modelOffset = GetElementByTagName(model, "offset").Slice("<sep>");     
                                string[] modelScale = GetElementByTagName(model, "scale").Slice("<sep>");    
                                string[] modelRotation = GetElementByTagName(model, "rotation").Slice("<sep>");   
                                string[] modelMaterials = GetElementByTagName(model, "materials").Slice("<mat>");

                                SetMeshRenderers(cloth, false);
                                GameObject PinaContainer = new GameObject();
                                MeshFilter meshFilter = PinaContainer.AddComponent<MeshFilter>();
                                MeshRenderer meshRenderer = PinaContainer.AddComponent<MeshRenderer>();
                                meshFilter.mesh = customAssets.meshes[modelIndex];
                                Material[] defaultMats = customAssets.materials[modelIndex];
                                Material[] newMaterials = new Material[defaultMats.Length];
                                for(int i = 0; i < meshRenderer.materials.Length; i++)
                                {
                                    Material chosen = null;
                                    if(modelMaterials[i] != "None")
                                    {
                                        string[] fields = modelMaterials[i].Slice("<col>");
                                        Material mat = new Material(Shader.Find("Standard"));
                                        mat.color = HexColor(fields[0]);
                                        Color emission = mat.color * ParseFloat(fields[1]);
                                        mat.SetColor("_EmissionColor", emission);
                                        mat.EnableKeyword("_EMISSION");
                                        mat.SetFloat("_Glossiness", 0f);
                                        chosen = mat;
                                    }
                                    else chosen = defaultMats[i];
                                    newMaterials[i] = chosen;
                                }
                                meshRenderer.materials = newMaterials;
                                PinaContainer.transform.position = cloth.transform.position;
                                PinaContainer.transform.rotation = cloth.transform.rotation;
                                PinaContainer.transform.localScale = new Vector3(ParseFloat(modelScale[0]), ParseFloat(modelScale[1]), ParseFloat(modelScale[2]));
                                PinaContainer.transform.SetParent(cloth.transform);
                                PinaContainer.transform.localPosition = new Vector3(ParseFloat(modelOffset[0]), ParseFloat(modelOffset[1]), ParseFloat(modelOffset[2]));
                                PinaContainer.transform.localRotation = Quaternion.Euler(new Vector3(ParseFloat(modelRotation[0]), ParseFloat(modelRotation[1]), ParseFloat(modelRotation[2])));
                                cloth.transform.SetHideFlagsChildren();
                            }
                            else if (materials != "None")
                            {
                                Renderer[] renderers = GetBestRenderers(cloth.gameObject);
                                Renderer best = GetBestRenderer(cloth.gameObject);
                                if (renderers != null && renderers.Length > 0)
                                {
                                    string[] mats = materials.Slice("<mat>");
                                    Material[] newMaterials = new Material[mats.Length];
                                    Dictionary<Color, Material> lodDictionary = new Dictionary<Color, Material>();
                                    
                                    for (int i = 0; i < newMaterials.Length; i++)
                                    {
                                        if (mats[i] != "None")
                                        {                                             
                                            string[] fields = mats[i].Slice("<col>");
                                            Material mat = new Material(Shader.Find("Standard"));
                                            mat.color = HexColor(fields[0]);
                                            Color emission = mat.color * ParseFloat(fields[1]);
                                            mat.SetColor("_EmissionColor", emission);
                                            mat.EnableKeyword("_EMISSION");
                                            mat.SetFloat("_Glossiness", 0f);
                                            newMaterials[i] = mat;
                                        }
                                        else newMaterials[i] = null;
                                    }

                                    Material[] bestMaterials = best.materials;
                                    if(bestMaterials != null && bestMaterials.Length > 0)
                                    {
                                        for (int i = 0; i < bestMaterials.Length && i < newMaterials.Length; i++)
                                        {
                                            if (newMaterials[i]) lodDictionary[bestMaterials[i].color] = newMaterials[i]; 
                                            else lodDictionary[bestMaterials[i].color] = null;
                                        }
                                    }

                                    foreach (Renderer renderer in renderers)
                                    {
                                        List<TeamColor> teamColors = renderer.GetComponents<TeamColor>().ToList();
                                        List<TeamColor> removed = new List<TeamColor>();
                                        Material[] rMaterials = renderer.materials;
                                        List<Material> myNewMaterials = new List<Material>();
                                        if(renderer.materials != null && renderer.materials.Length > 0)
                                        {
                                            for (int i = 0; i < rMaterials.Length; i++)
                                            {
                                                Material replacement = null;
                                                if(lodDictionary.ContainsKey(rMaterials[i].color)) replacement = lodDictionary[rMaterials[i].color];
                                                if (replacement)
                                                {                  
                                                    myNewMaterials.Add(replacement);  
                                                    foreach(TeamColor tc in teamColors) 
                                                    {
                                                        if(!removed.Contains(tc) && tc.materialID == i)
                                                        {
                                                            removed.Add(tc);
                                                            UnityEngine.Object.DestroyImmediate(tc);
                                                        }
                                                    }
                                                }
                                                else myNewMaterials.Add(rMaterials[i]);
                                            }
                                        }
                                        renderer.materials = myNewMaterials.ToArray();
                                    }
                                }
                            }
                        }
                    }
                }
                unit.m_props = clothingObjects.ToArray();
                if (doStepTest) Debug.Log("[STEPTEST] cloth");

                if(riders != "None")
                {
                    WebClient client = new WebClient();
                    List<UnitBlueprint> riderBlueprints = new List<UnitBlueprint>();
                    string[] riderDataSets = riders.Slice("<rdr>");
                    foreach(string riderData in riderDataSets)
                    {
                        string[] riderSep = riderData.Slice("<sep>");
                        string type = riderSep[0];
                        bool custom = (riderSep[1] == "true");
                        if(!custom) riderBlueprints.Add((UnitBlueprint)GetVDicItem("blueprints", type));
                        else if(riderDataDictionary != null)
                        {
                            if(!(from (string key, UnitBlueprint blueprint) keyBlueprintPair in returnList select keyBlueprintPair.key).Contains(type))
                            {
                                string riderUnitData = riderDataDictionary[type];
                                if (riderUnitData != "<loaded>") riderBlueprints.Add(DeserializeUnit(type, riderUnitData, blueprintDictionary, riderDataDictionary, doStepTest, returnList)[0].blueprint);
                                else if(blueprintDictionary.ContainsKey(type)) riderBlueprints.Add(blueprintDictionary[type]);
                            }
                        }
                    }
                    unit.Riders = riderBlueprints.ToArray();
                }
                if (doStepTest) Debug.Log("[STEPTEST] riders");

                var moveObjects = new List<GameObject>();
                foreach (string moveName in combatMove)
                {
                    if (moveName != "None" && moveName != "") moveObjects.Add((GameObject)GetVDicItem("moves", moveName));
                }
                if (doStepTest) Debug.Log("[STEPTEST] moves");
                unit.objectsToSpawnAsChildren = moveObjects.ToArray();
                if (sprite != "None") unit.Entity.SpriteIcon = (Sprite)GetVDicItem("sprites", sprite);
                unit.holdinigWithTwoHands = (twoHands == "true");
                unit.health = ParseFloat(unitHealth);
                unit.forceCost = (ushort)ParseFloat(unitCost);
                unit.movementSpeedMuiltiplier = ParseFloat(unitSpeed);
                unit.turnSpeed *= ParseFloat(unitTurn);
                unit.sizeMultiplier = ParseFloat(unitSize);
                unit.massMultiplier = ParseFloat(unitMass);
                unit.balanceMultiplier = ParseFloat(unitBalance);
                unit.balanceForceMultiplier = ParseFloat(unitForce);
                returnList.Add((unitID, unit));
                returnList.Reverse();
                return returnList.ToArray();
            }
            catch(Exception exception)
            {
                if(doStepTest) Debug.Log("[WEBTABS] Aysnc Thread Error:\n"+exception.ToString());
                return null;
            }
        }
    }
}
