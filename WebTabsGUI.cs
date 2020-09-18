using System;
using System.Linq;
using System.IO;
using Landfall.TABS;
using System.Net;
using System.Collections;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using Landfall.TABS.GameMode;
using Landfall.TABS.GameState;
using Landfall.TABS.UnitPlacement;
using UModLoader;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Threading.Tasks;

namespace WebTabs
{
    public class WebTabsGUI : MonoBehaviour
    {
        private static Faction faction;

        private static GameObject canvas;

        private static WebTabsGUI instance;

        private static GUIStyle[] styles;

        private static Texture2D background;

        private static Color backgroundColor;

        private static string unitID;

        private static string factionID;

        private static string output;

        private static Dictionary<string, UnitBlueprint> unitDictionary;

        private static Dictionary<string, bool> factionUnit;

        private static Dictionary<DatabaseID, string> loadedUnits;

        private static Dictionary<string, UnitBlueprint> deletedUnits;

        private static Dictionary<DatabaseID, string> loadedFactions;

        private static Dictionary<string, Faction> deletedFactions;
        private static bool enableGUI = true;
        public static void init()
        {
            if (WebTabsSettings.printData) WebUtils.PrintDataFiles();
            if(!instance)
            {
                instance = new GameObject { hideFlags = HideFlags.DontSave }.AddComponent<WebTabsGUI>();
                unitID = string.Empty;
                factionID = string.Empty;
                faction = UFunctions.CreateFaction("WebTabs", null, DataContainer.GetIcon());
                faction.Units = new UnitBlueprint[0];
                faction.index = int.MaxValue;

                unitDictionary = new Dictionary<string, UnitBlueprint>();
                factionUnit = new Dictionary<string, bool>();
                loadedUnits = new Dictionary<DatabaseID, string>();
                deletedUnits = new Dictionary<string, UnitBlueprint>();
                loadedFactions = new Dictionary<DatabaseID, string>();
                deletedFactions = new Dictionary<string, Faction>();
                styles = null;
                (from Faction factionS in WebTabsSettings.database.Factions where (factionS.Entity.Name == "Secret") select factionS).ToArray()[0].index = int.MaxValue-1;
                LoadData();
            }
        }

        private void OnGUI()
        {
            if(styles == null)
            {
                styles =  new GUIStyle[]
                {
                    new GUIStyle(GUI.skin.textField){ fontSize = (int)(0.02f * (float)Screen.height)},
                    new GUIStyle(GUI.skin.box){ fontSize = (int)(0.04f * (float)Screen.height)},
                    new GUIStyle(GUI.skin.button) { fontSize = (int)(0.02f * (float)Screen.height)},
                    new GUIStyle(GUI.skin.label){ fontSize = (int)(0.014f * (float)Screen.height)},
                    new GUIStyle(GUI.skin.box){ fontSize = (int)(0.025f * (float)Screen.height)}
                };
                backgroundColor = new Color32(51, 100, 51, 255);
                background = new Texture2D(1, 1);
                background.SetPixel(0, 0, new Color(1, 1, 1, 1));
                background.Apply();
            }
            else
            {
                styles[0].fontSize = (int)(0.02f * (float)Screen.height);
                styles[1].fontSize = (int)(0.04f * (float)Screen.height);
                styles[2].fontSize = (int)(0.02f * (float)Screen.height);
                styles[3].fontSize = (int)(0.014f * (float)Screen.height);
                styles[4].fontSize = (int)(0.025f * (float)Screen.height);
                styles[1].normal.background = background;
                styles[4].normal.background = background;
                GUI.backgroundColor = backgroundColor;
            }

            GameModeService gameModeService = ServiceLocator.GetService<GameModeService>();
            GameStateManager gameModeManager = ServiceLocator.GetService<GameStateManager>();
            PlacementUI pUI = FindObjectOfType<PlacementUI>();
            Faction selectedFaction = null;
            if (pUI) selectedFaction = (Faction)pUI.GetField("m_selectedFaction");
            if (gameModeService && (gameModeManager.GameState == GameState.PlacementState) && (pUI) && selectedFaction && (selectedFaction.Entity.Name == faction.Entity.Name || loadedFactions.ContainsKey(((Faction)pUI.GetField("m_selectedFaction")).Entity.GUID)) && enableGUI)
            {
                UnitPlacementBrush brush = (UnitPlacementBrush)pUI.GetField("m_unitPlacementBrush");
                UnitBlueprint selectedUnit = brush.UnitToSpawn;
                if (!canvas) canvas = new GameObject("WebTabsCanvas");
                canvas.SetActive(true);
                Canvas gameCanvas = FindObjectOfType<Canvas>();
                if (gameCanvas)
                {
                    canvas.transform.SetParent(gameCanvas.transform);
                    canvas.FetchComponent<EventSystem>();
                    canvas.FetchComponent<Image>().color = new Color32(4, 36, 20, 255);
                    RectTransform rt = canvas.FetchComponent<RectTransform>();
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.99f, 0.935f);
                    rt.anchorMin = new Vector2(0.79f, 0.41f);
                    rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta = new Vector2(1, 1);
                }
                GUI.Box(new Rect(0.79f * (float)Screen.width, 0.06f * (float)Screen.height, 0.2f * (float)Screen.width, 0.15f * (float)Screen.height), "WebTabs", styles[1]);

                GUI.Label(new Rect(0.81f * (float)Screen.width, 0.105f * (float)Screen.height, 0.05f * (float)Screen.width, 0.04f * (float)Screen.height), "Unit Key:", styles[3]);
                unitID = GUI.TextField(new Rect(0.858f * (float)Screen.width, 0.108f * (float)Screen.height, 0.05f * (float)Screen.width, 0.025f * (float)Screen.height), unitID, styles[0]).ToLower();
                if (unitID.Length > 8) unitID = unitID.Substring(0, 8);

                if (GUI.Button(new Rect(0.82f * (float)Screen.width, 0.14f * (float)Screen.height, 0.15f * (float)Screen.width, 0.02f * (float)Screen.height), "Get Unit", styles[2])) GetUnits(new string[] { unitID });

                if (GUI.Button(new Rect(0.82f * (float)Screen.width, 0.16f * (float)Screen.height, 0.15f * (float)Screen.width, 0.02f * (float)Screen.height), ((selectedUnit && Array.Exists(faction.Units, element => element == selectedUnit)) ? "Delete '" + selectedUnit.Entity.Name + "'" : "No unit selected"), styles[2]) && (selectedUnit && Array.Exists(faction.Units, element => element == selectedUnit) && selectedFaction == faction))
                {
                    output = "Deleting '" + selectedUnit.Entity.Name + "'...\n";
                    string deleteID = loadedUnits[selectedUnit.Entity.GUID];
                    deletedUnits[deleteID] = selectedUnit;
                    loadedUnits.Remove(selectedUnit.Entity.GUID);
                    UFunctions.RemoveUnitFromFaction(selectedUnit, faction);
                    pUI.RedrawFactionUnits(faction);
                    brush.ClearBrushUnit();
                    foreach (Unit unit in (from Unit unit in FindObjectsOfType<Unit>() where unit.unitBlueprint == selectedUnit select unit)) brush.RemoveUnitInternal(unit, unit.Team);
                    output += "Deleted unit successfully!";
                }
                GUI.Box(new Rect(0.79f * (float)Screen.width, 0.25f * (float)Screen.height, 0.2f * (float)Screen.width, 0.15f * (float)Screen.height), "Factions", styles[4]);

                GUI.Label(new Rect(0.81f * (float)Screen.width, 0.295f * (float)Screen.height, 0.05f * (float)Screen.width, 0.04f * (float)Screen.height), "Faction Key:", styles[3]);
                factionID = GUI.TextField(new Rect(0.858f * (float)Screen.width, 0.298f * (float)Screen.height, 0.05f * (float)Screen.width, 0.025f * (float)Screen.height), factionID, styles[0]).ToLower();
                if (factionID.Length > 8) factionID = factionID.Substring(0, 8);

                if (GUI.Button(new Rect(0.82f * (float)Screen.width, 0.33f * (float)Screen.height, 0.15f * (float)Screen.width, 0.02f * (float)Screen.height), "Get Faction", styles[2])) GetFactions(new string[] { factionID });

                if (GUI.Button(new Rect(0.82f * (float)Screen.width, 0.35f * (float)Screen.height, 0.15f * (float)Screen.width, 0.02f * (float)Screen.height), ((selectedFaction && selectedFaction != faction) ? "Delete '" + selectedFaction.Entity.Name + "'" : "No faction selected"), styles[2]) && (selectedFaction) && selectedFaction != faction)
                {
                    output = "Deleting '" + selectedFaction.Entity.Name + "'...\n";
                    string deleteID = loadedFactions[selectedFaction.Entity.GUID];
                    deletedFactions[deleteID] = selectedFaction;
                    loadedFactions.Remove(selectedFaction.Entity.GUID);
                    selectedFaction.m_displayFaction = false;
                    pUI.SelectFaction();
                    pUI.RedrawFactions();
                    brush.ClearBrushUnit();
                    output += "Deleted faction successfully!";
                }

                GUI.Box(new Rect(0.79f * (float)Screen.width, 0.44f * (float)Screen.height, 0.2f * (float)Screen.width, 0.07f * (float)Screen.height), "Save All Data", styles[4]);

                if (GUI.Button(new Rect(0.82f * (float)Screen.width, 0.48f * (float)Screen.height, 0.15f * (float)Screen.width, 0.02f * (float)Screen.height), "Save", styles[2])) SaveData();

                GUI.Box(new Rect(0.79f * (float)Screen.width, 0.53f * (float)Screen.height, 0.2f * (float)Screen.width, 0.07f * (float)Screen.height), "Console", styles[4]);

                GUI.TextField(new Rect(0.81f * (float)Screen.width, 0.56f * (float)Screen.height, 0.2f * (float)Screen.width, 0.04f * (float)Screen.height), output, styles[3]);
            }
            else if (canvas) canvas.SetActive(false);
        }

        private static async Task GetUnits(string[] unitCodes, Faction destination = null, bool resetResult = true)
        {
            if(resetResult) output = "";
            if (!destination) destination = faction;
            foreach(string unitCode in unitCodes)
            {
                try
                {
                    if (unitCode != "")
                    {
                        output += "Loading unit '" + unitCode + "'...\n";
                        if (!loadedUnits.ContainsValue(unitCode) && !deletedUnits.ContainsKey(unitCode))
                        {
                            string unitData = await WebTabsSettings.webClient.DownloadStringTaskAsync(new Uri(WebTabsSettings.serverURL + "units\\" + unitCode + ".txt"));
                            string riders = WebUtils.GetElementByTagName(unitData, "riders");
                            Dictionary<string, string> dataDictionary = null;
                            if(riders != "None")
                            {
                                dataDictionary = new Dictionary<string, string>();
                                string[] riderData = riders.Slice("<rdr>");
                                List<string> riderIDQueue = (from string riderDatum in riderData where riderDatum.Slice("<sep>")[1] == "true" select riderDatum.Slice("<sep>")[0]).ToList();

                                for(int i = 0; i < riderIDQueue.Count && i < 50; i++)
                                {
                                    string riderID = riderIDQueue[i];
                                    string riderUnitData = string.Empty;
                                    if(unitDictionary.Keys.Contains(riderID)) riderUnitData = "<loaded>";
                                    else 
                                    {
                                        riderUnitData = await WebTabsSettings.webClient.DownloadStringTaskAsync(new Uri(WebTabsSettings.serverURL + "units\\" + riderID + ".txt"));
                                        string riderRiders = WebUtils.GetElementByTagName(riderUnitData, "riders");
                                        if(riderRiders != "None")
                                        {
                                            IEnumerable<string> riderRiderIDS = (from string riderRiderDatum in riderRiders.Slice("<rdr>") where riderRiderDatum.Slice("<sep>")[1] == "true" select riderRiderDatum.Slice("<sep>")[0]);
                                            foreach(string riderRiderID in riderRiderIDS) riderIDQueue.Add(riderRiderID);
                                        }
                                    }
                                    dataDictionary.Add(riderID, riderUnitData);
                                }
                            }
                            (string key, UnitBlueprint blueprint)[] units = WebUtils.DeserializeUnit(unitCode, unitData, unitDictionary, dataDictionary, WebTabsSettings.doStepTest);
                            for(int i = 0; i < units.Length; i++)
                            {
                                UnitBlueprint unit = units[i].blueprint;
                                string key = units[i].key;
                                WebTabsSettings.database.AddUnitWithID(unit);
                                unitDictionary[key] = unit;
                                if(i == 0)
                                {
                                    UFunctions.AddUnitToFaction(unit, destination);
                                    loadedUnits[unit.Entity.GUID] = key;
                                    factionUnit[key] = !(destination == faction);
                                }
                                else
                                {
                                    deletedUnits[key] = unit;
                                    factionUnit[key] = false;
                                }
                            }

                            output += "Unit loaded successfully!\n";
                        }
                        else if (deletedUnits.ContainsKey(unitCode))
                        {
                            UnitBlueprint unit = deletedUnits[unitCode];
                            UFunctions.AddUnitToFaction(unit, destination);
                            loadedUnits[unit.Entity.GUID] = unitCode;
                            deletedUnits.Remove(unitCode);
                            output += "Unit loaded successfully!\n";
                        }
                        else if(destination != faction ^ !Array.Exists(faction.Units, element => element == unitDictionary[unitCode]))
                        {
                            if (destination == faction) factionUnit[unitCode] = false;
                            UFunctions.AddUnitToFaction(unitDictionary[unitCode], destination);
                            output += "Unit loaded successfully!\n";
                        }
                        else
                        {
                            Debug.Log("[WEBTABS] Unit with ID '" + unitCode + "' has already been loaded.");
                            output += "Unit with ID '" + unitCode + "' has already been loaded.\n";
                        }
                        PlacementUI pUI = FindObjectOfType<PlacementUI>();
                        if (pUI) pUI.RedrawFactionUnits(destination);

                    }
                    else output += "Please provide a unit key.\n";
                }
                catch (WebException)
                {
                    Debug.Log("[WEBTABS] Unit with ID '" + unitCode + "' does not exist.");
                    output += "Unit with ID '" + unitCode + "' could not be found.\n";
                }
            }
        }

        private static async void GetFactions(string[] factionCodes)
        {
            output = "";
            foreach(string factionCode in factionCodes)
            {
                try
                {
                    if (factionCode != "")
                    {
                        if(!loadedFactions.ContainsValue(factionCode) && !deletedFactions.ContainsKey(factionCode))
                        {
                            output += "Loading faction '" + factionCode + "'...\n";
                            string factionData = await WebTabsSettings.webClient.DownloadStringTaskAsync(new Uri(WebTabsSettings.serverURL + "factions\\" + factionCode + ".txt"));
                            string name = WebUtils.GetElementByTagName(factionData, "name");
                            string sprite = WebUtils.GetElementByTagName(factionData, "sprite");
                            string[] units = WebUtils.GetElementByTagName(factionData, "units").Slice("<sep>");
                            Sprite spriteOBJ = null;
                            if (sprite != "" && sprite != "None") spriteOBJ = (Sprite)ULoader.VDic["sprites"][sprite];
                            Faction newFaction = UFunctions.CreateFaction(name, null, spriteOBJ);
                            loadedFactions[newFaction.Entity.GUID] = factionCode;
                            WebTabsSettings.database.AddFactionWithID(newFaction);
                            await GetUnits(units, newFaction, false);

                            PlacementUI pUI = FindObjectOfType<PlacementUI>();
                            if (pUI)
                            {
                                pUI.RedrawFactions();
                                pUI.RedrawFactionUnits(newFaction);
                            }
                            output += "Faction loaded successfully!\n";
                        }
                        else if(deletedFactions.ContainsKey(factionCode))
                        {
                            Faction factionD = deletedFactions[factionCode];
                            loadedFactions[factionD.Entity.GUID] = factionCode;
                            deletedUnits.Remove(factionCode);
                            factionD.m_displayFaction = true;
                            PlacementUI pUI = FindObjectOfType<PlacementUI>();
                            if (pUI) pUI.RedrawFactions();
                            output += "Faction loaded successfully!\n";
                        }
                        else
                        {
                            Debug.Log("[WEBTABS] Faction with ID '" + factionCode + "' has already been loaded.");
                            output += "Faction with ID '" + factionCode + "' has already been loaded.\n";
                        }
                    }
                    else output += "Please provide a faction key.\n";
                }
                catch(WebException)
                {
                    Debug.Log("[WEBTABS] Faction with ID '" + factionCode + "' does not exist.");
                    output += "Faction with ID '" + factionCode + "' could not be found.\n";
                }
            }
        }

        private static void SaveData()
        {
            output = "";
            output += "Saving...\n";
            string[] ids = loadedUnits.Values.ToArray();
            string[] unIDS = (from string id in ids where !factionUnit[id] select id).ToArray();
            string[] facIds = loadedFactions.Values.ToArray();
            string unitIDS = string.Empty;
            string factionIDS = string.Empty;
            for (int i = 0; i < unIDS.Length; i++) unitIDS += unIDS[i] + ((i < unIDS.Length - 1) ? "<sep>" : "");
            for (int i = 0; i < facIds.Length; i++) factionIDS += facIds[i] + ((i < facIds.Length - 1) ? "<sep>" : "");
            string saveData =
            @"
                    <save>
                        <units><UDAT><units>
                        <factions><FDAT><factions>
                    <save>
                    ".Replace("<UDAT>", unitIDS).Replace("<FDAT>", factionIDS);
            File.WriteAllText("TotallyAccurateBattleSimulator_Data\\WebTabsUnits.txt", saveData);
            output += "Saved sucessfully!\n";
        }

        private static async void LoadData()
        {
            if (File.Exists("TotallyAccurateBattleSimulator_Data\\WebTabsUnits.txt"))
            {
                string rawData = File.ReadAllText("TotallyAccurateBattleSimulator_Data\\WebTabsUnits.txt");
                if (rawData.Contains("<unit>"))
                {
                    string[] sections = (from string section in rawData.Slice("<sep>") select WebUtils.GetElementByTagName(section, "unit")).ToArray();
                    await GetUnits(sections);
                }
                else if (rawData.Contains("<units>"))
                {
                    string[] units = WebUtils.GetElementByTagName(rawData, "units").Slice("<sep>");
                    string[] factions = WebUtils.GetElementByTagName(rawData, "factions").Slice("<sep>");
                    await GetUnits(units);
                    GetFactions(factions);
                    
                }
                else Debug.Log("[WEBTABS] Save data was either empty or corrupt.");
            }
        }

        private void Update()
        {
            if((Input.GetKeyDown(KeyCode.G) || Input.GetKeyDown(KeyCode.BackQuote) || Input.GetKeyDown(KeyCode.Tilde)) && (SceneManager.GetActiveScene().path != "Assets/Scenes/WebTabsUnitPreview.unity" || SceneManager.GetActiveScene().path != "Assets/11 Scenes/MainMenu_GamepadUI.unity")) enableGUI = !enableGUI;
        }
    }
}
