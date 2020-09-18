using System.Threading.Tasks;
using UnityEngine;
using UE = UnityEngine;
using UnityEngine.SceneManagement;
using System.Net;
using System.Collections.Generic;
using Landfall.TABS;
using UModLoader;
using System.IO;
using Landfall.TABS.Workshop;
using TMPro;
using System.Text.RegularExpressions;
using UnityEngine.UI;
using System;
using System.Linq;
using UnityEngine.EventSystems;

namespace WebTabs
{
    public class WebTabsServer
    {
        private static LandfallUnitDatabase database = LandfallUnitDatabase.GetDatabase();

        private static HttpListenerContext initialContext;
        private static HttpListener listener;
        private static bool running = false;

        private static bool hasRequesites;

        private static Unit current;
        private static Team unitTeam = Team.Red;
        private static int revisionNumber = 1;
        private static GameObject cameraObject;
        private static string lastData = "";

        private static Dictionary<string, UnitBlueprint> unitDictionary = new Dictionary<string, UnitBlueprint>();

        public static async Task HandleRequests()
        {
            while(running)
            {
                
                HttpListenerContext context = await listener.GetContextAsync();
                HttpListenerRequest request = context.Request;
                if(context.Request.HttpMethod == "POST")
                {
                    string postData = GetPostData(request);
                    if(postData.Slice("<div>")[0] == "<connect>")
                    {
                        string[] pdSliced = postData.Slice("<div>");
                        if(initialContext != null) WebTabsPreviewGUI.output = "Reconnected to local WebTabs session!";
                        else WebTabsPreviewGUI.output = "Established Connection with local WebTabs session!";
                        initialContext = context;
                        string makeUnitResult = WebUtils.GetElementByTagName(pdSliced[1], "unitdata");
                        string makeUnitMessage = WebUtils.GetElementByTagName(pdSliced[1], "message");
                        if(makeUnitResult != "error") HandleUnit(makeUnitResult);
                        else HandleError(makeUnitMessage);

                    }
                    else if (initialContext != null)
                    {
                        if(lastData == "" || lastData != postData)
                        {
                            lastData = postData;
                            string makeUnitResult = WebUtils.GetElementByTagName(postData, "unitdata");
                            string makeUnitMessage = WebUtils.GetElementByTagName(postData, "message");
                            if(makeUnitResult != "error") HandleUnit(makeUnitResult);
                            else HandleError(makeUnitMessage);
                        }
                    }
                    else context.Response.Abort();
                }
            }
        }


        public static async void Start()
        {
            SceneManager.sceneLoaded += SetupScene;
            running = true;
            listener = new HttpListener();
            listener.Prefixes.Add(WebTabsSettings.clientURL);
            listener.Start();
            Debug.Log("[WEBTABS] WebTabs webserver started!");
            await HandleRequests();
            listener.Close();
            Debug.Log("[WEBTABS] WebTabs webserver has been terminated.");
        }

        public static void Kill()
        {
            running = false;
            Disconnect();
            lastData = "";
        }

        public static void Disconnect()
        { 
            initialContext.Response.Abort(); 
            initialContext = null;
        }

        private static string GetPostData(HttpListenerRequest request)
        {
            StreamReader disp = new StreamReader(request.InputStream, request.ContentEncoding);
            string data = System.Web.HttpUtility.UrlDecode(disp.ReadToEnd());
            disp.Close();
            return data;
        }

        public static void LoadUnitPreviewScene()
        {
            CampaignPlayerDataHolder.BackToMenu();
            CampaignHandler.ResetLoadedLevel();
            TABSSceneManager.LoadScene("WebTabsUnitPreview", false);
        }

        public static async void HandleUnit(string unitData)
        {
            string unitCode = ("prev#"+revisionNumber++);
            string riders = WebUtils.GetElementByTagName(unitData, "riders");
            Dictionary<string, string> dataDictionary = null;
            if(riders != "None" && riders != "")
            {
                dataDictionary = new Dictionary<string, string>();
                string[] riderData = riders.Slice("<rdr>");
                List<string> riderIDQueue = (from string riderDatum in riderData where riderDatum.Slice("<sep>")[1] == "true" select riderDatum.Slice("<sep>")[0]).ToList();

                for(int i = 0; i < riderIDQueue.Count && i < 50; i++)
                {
                    string riderID = riderIDQueue[i];
                    string riderUnitData = string.Empty;
                    if(unitDictionary.ContainsKey(riderID)) riderUnitData = "<loaded>";
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
                    dataDictionary[riderID] = riderUnitData;
                }
            }
            (string key, UnitBlueprint blueprint)[] units = WebUtils.DeserializeUnit(unitCode, unitData, unitDictionary, dataDictionary, WebTabsSettings.doStepTest);
            for(int i = 0; i < units.Length; i++)
            {
                UnitBlueprint unit = units[i].blueprint;
                database.AddUnitWithID(unit);
                if(i > 0)
                {
                    string key = units[i].key;
                    unitDictionary[key] = unit;
                }
            }
            UnitBlueprint blueprint = units[0].blueprint;
            if(current) current.DestroyUnit();
            current = blueprint.Spawn(GameObject.Find("SpawnUnit").transform.position, Quaternion.identity, unitTeam)[0].GetComponent<Unit>();
            cameraObject.GetComponent<MonoUtilities.LerpPosition>().follow = current.data.hip.transform;
            cameraObject.GetComponentInChildren<Camera>().gameObject.GetComponent<MonoUtilities.LookAtTransform>().follow = current.data.hip.transform;
            cameraObject.GetComponentInChildren<Camera>().gameObject.GetComponent<MonoUtilities.CameraDolly>().follow = current.data.hip.transform;
        }

        public static void HandleError(string errorData)
        {
            errorData = errorData.Replace("<span error='error'>", "");
            errorData = errorData.Replace("</span>", "");
            errorData = errorData.Replace("<br>", "\n");
            WebTabsPreviewGUI.output = errorData;
        }

        private static void SetupScene(Scene scene, LoadSceneMode mode)
        {
            if(scene.path == "Assets/Scenes/WebTabsUnitPreview.unity")
            {
                Time.timeScale = .5f;
                GameObject oldMC = GameObject.Find("Main Camera");
                UE.Object.Instantiate(WebTabsSettings.myPool.GetObject("menuPPV"));
                Color oldCamCol = oldMC.GetComponent<Camera>().backgroundColor;
                UE.Object.Destroy(oldMC);
                GameObject TABCCamera = UE.Object.Instantiate(WebTabsSettings.myPool.GetObject("cameraTABC"));
                Camera actualCamera = TABCCamera.GetComponentInChildren<Camera>();
                actualCamera.transform.position = new Vector3(0, 0, 4.5f);
                actualCamera.backgroundColor = oldCamCol;
                GameObject.Find("Directional Light").transform.parent = TABCCamera.transform;
                UE.Object.Instantiate(WebTabsSettings.myPool.GetObject("MSETOBJECT"));
                var spawnUnitSC = GameObject.Find("SpawnUnit").AddComponent<MonoUtilities.SimpleEscapeToMenu>();
                spawnUnitSC.gameObject.AddComponent<WebTabsPreviewGUI>();
                cameraObject = TABCCamera;
            }
            else if(scene.path == "Assets/11 Scenes/MainMenu.unity")
            {
                HideIfEA earlyAccessHider = UE.Object.FindObjectOfType<HideIfEA>();
                if(earlyAccessHider)
                {
                    Image image = earlyAccessHider.transform.Find("SlicedImage").GetComponent<Image>();
                    Button button = earlyAccessHider.GetComponent<Button>();
                    TextMeshProUGUI text = earlyAccessHider.GetComponentInChildren<TextMeshProUGUI>();
                    earlyAccessHider.gameObject.FetchComponent<UIScaleJiggle>().enabled = true;
                    earlyAccessHider.gameObject.FetchComponent<UISounds>().enabled = true;
                    button.interactable = true;
                    image.color = new Color(image.color.r, image.color.g, image.color.b, 1f);
                    text.color = new Color(text.color.r, text.color.g, text.color.b, 1f);
                    button.onClick.AddListener(LoadUnitPreviewScene);
                }
                if(!hasRequesites)
                {
                    GameObject cameraTABC = WebTabsSettings.myPool.AddObject("cameraTABC", UE.Object.FindObjectOfType<Camera>().transform.root.gameObject);
                    cameraTABC.name = "WebTabsCamera";
                    UE.Object.Destroy(cameraTABC.GetComponent<IntroSequence>());
                    while(cameraTABC.GetComponent<Rotate>()) UE.Object.DestroyImmediate(cameraTABC.GetComponent<Rotate>());
                    UE.Object.Destroy(cameraTABC.GetComponent<MainMenuCameraMove>());
                    UE.Object.Destroy(cameraTABC.GetComponent<MainMenuTimeHandler>());
                    foreach(Transform child in cameraTABC.GetComponentInChildren<Camera>().transform) UE.Object.Destroy(child.gameObject);
                    cameraTABC.AddComponent<MonoUtilities.SwipeRotate>();
                    cameraTABC.AddComponent<MonoUtilities.LerpPosition>().followSpeed = 10f;
                    MonoUtilities.CameraDolly cameraDolly = cameraTABC.GetComponentInChildren<Camera>().gameObject.AddComponent<MonoUtilities.CameraDolly>();
                    cameraDolly.sensitivity = 150f;
                    cameraDolly.updateSpeed = 40f;
                    cameraDolly.bounds = new Vector2(1f, 30f);
                    cameraDolly.gameObject.AddComponent<MonoUtilities.LookAtTransform>().followSpeed = 10f;
                    cameraTABC.transform.position = Vector3.zero;
                    cameraTABC.transform.GetChild(0).position = Vector3.zero;
                    cameraTABC.transform.SetHideFlagsChildren();
                    WebTabsSettings.myPool.AddObject("MSETOBJECT", new GameObject("MSETOBJECT", typeof(GooglyEyes), typeof(MapSettings), typeof(SceneSettings)), false);
                    WebTabsSettings.myPool.AddObject("menuPPV", GameObject.Find("Post-process Volume"));
                    hasRequesites = true;
                }
            }
        }

        public class WebTabsPreviewGUI : MonoBehaviour
        {
            private static GUIStyle[] styles;

            private static Texture2D background;

            private static Color backgroundColor;

            private static GameObject canvas;

            private bool enableGUI = true;

            public static string output;
            private void OnGUI()
            {
                int w = Screen.width;
                int h = Screen.height;

                if(styles == null)
                {
                    styles =  new GUIStyle[]
                    {
                        new GUIStyle(GUI.skin.textField){ fontSize = (int)(0.02f * (float)h)},
                        new GUIStyle(GUI.skin.box){ fontSize = (int)(0.04f * (float)h)},
                        new GUIStyle(GUI.skin.button) { fontSize = (int)(0.02f * (float)h)},
                        new GUIStyle(GUI.skin.label){ fontSize = (int)(0.014f * (float)h)},
                        new GUIStyle(GUI.skin.box){ fontSize = (int)(0.025f * (float)h)}
                    };
                    backgroundColor = new Color32(51, 100, 51, 255);
                    background = new Texture2D(1, 1);
                    background.SetPixel(0, 0, new Color(1, 1, 1, 1));
                    background.Apply();
                }
                else
                {
                    styles[0].fontSize = (int)(0.02f * (float)h);
                    styles[1].fontSize = (int)(0.04f * (float)h);
                    styles[2].fontSize = (int)(0.02f * (float)h);
                    styles[3].fontSize = (int)(0.014f * (float)h);
                    styles[4].fontSize = (int)(0.025f * (float)h);
                    styles[1].normal.background = background;
                    styles[4].normal.background = background;
                    GUI.backgroundColor = backgroundColor;
                }


                if (enableGUI)
                {

                    if (!canvas) canvas = new GameObject("WebTabsPreviewCanvas");
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
                        rt.anchorMin = new Vector2(0.79f, 0.68f);
                        rt.anchoredPosition = Vector2.zero;
                        rt.sizeDelta = new Vector2(1, 1);
                    }
                    GUI.Box(new Rect(0.79f * (float)w, 0.06f * (float)h, 0.2f * (float)w, 0.075f * (float)h), "WebTabs", styles[1]);

                    GUI.Box(new Rect(0.79f * (float)w, 0.15f * (float)h, 0.2f * (float)w, 0.075f * (float)h), "Unit Team", styles[4]);
                    bool doSwitchUnit = GUI.Button(new Rect(0.84f * (float)w, 0.18f * (float)h, 0.1f * (float)w, 0.0375f * (float)h), "Switch Team", styles[2]);
                    if(doSwitchUnit)
                    {
                        if(current)
                        {
                            bool isRed = (unitTeam == Team.Red);
                            unitTeam = (isRed ? Team.Blue : Team.Red);
                            UnitBlueprint blueprint = current.unitBlueprint;
                            if(current) current.DestroyUnit();
                            current = blueprint.Spawn(GameObject.Find("SpawnUnit").transform.position, Quaternion.identity, unitTeam)[0].GetComponent<Unit>();
                            cameraObject.GetComponent<MonoUtilities.LerpPosition>().follow = current.data.hip.transform;
                            cameraObject.GetComponentInChildren<Camera>().gameObject.GetComponent<MonoUtilities.LookAtTransform>().follow = current.data.hip.transform;
                            cameraObject.GetComponentInChildren<Camera>().gameObject.GetComponent<MonoUtilities.CameraDolly>().follow = current.data.hip.transform;
                            output = "Changed unit team to "+(isRed ? "blue" : "red") + "!";
                        }
                        else output = "You must be connected to your WebTabs session to use this.";
                    }

                    GUI.Box(new Rect(0.79f * (float)w, 0.25f * (float)h, 0.2f * (float)w, 0.07f * (float)h), "Console", styles[4]);

                    GUI.TextField(new Rect(0.81f * (float)w, 0.28f * (float)h, 0.2f * (float)w, 0.04f * (float)h), output, styles[3]);
                }
                else if (canvas) canvas.SetActive(false);
            }

            private void Update()
            {
                if(Input.GetKeyDown(KeyCode.Escape)) enableGUI = false;

                if(Input.GetKeyDown(KeyCode.G) || Input.GetKeyDown(KeyCode.BackQuote) || Input.GetKeyDown(KeyCode.Tilde)) enableGUI = !enableGUI;
            }
        }
    }
}