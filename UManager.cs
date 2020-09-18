using System;
using System.Linq;
using Landfall.TABS;
using System.Collections.Generic;
using UnityEngine;
using WebTabs;

namespace UMods
{ 
    public class UManager
    {
        public void Init(LandfallUnitDatabase db)
        {
            Assets.initialize();
            WebTabsServer.Start();
            WebTabsGUI.init();
        }

        public void CodeOnSpawn(Unit unit, LandfallUnitDatabase db)
        {
            WebTabsID webTabsID = unit.GetComponent<WebTabsID>();
            if(webTabsID)
            {
                if (webTabsID && WebUtils.scaleDictionaries.ContainsKey(webTabsID.id))
                {
                    var boneDict = WebUtils.scaleDictionaries[webTabsID.id];
                    foreach (string key in boneDict.Keys)
                    {
                        if (key != null && boneDict.ContainsKey(key))
                        {
                            unit.ScaleBodyPart(key, boneDict[key]);
                        }
                    }
                }
            }
        }

        public void CodeAfterSpawn(Unit unit, LandfallUnitDatabase db)
        {
            WebTabsID webTabsID = unit.GetComponent<WebTabsID>();
            if(webTabsID)
            {
                if (WebUtils.eyeDictionary.ContainsKey(webTabsID.id))
                {
                    Transform[] eyeObjects = (new List<Transform>(from GooglyEye eye in unit.GetComponentsInChildren<GooglyEye>() select eye.transform)).ToArray();
                    if (eyeObjects != null)
                    {
                        bool tff = true;
                        var eyeMats = WebUtils.eyeDictionary[webTabsID.id];
                        if(eyeMats != null)
                        {
                            foreach (Transform parent in eyeObjects)
                            {
                                Tuple<Material, Material> materials = (tff ? eyeMats.Item1 : eyeMats.Item2);
                                if (materials != null)
                                {
                                    Transform white =  parent.FindChildRecursive("White");
                                    Transform pupil =  parent.FindChildRecursive("Pupil");
                                    if (white) white.GetComponent<MeshRenderer>().material = materials.Item1;
                                    if (pupil) pupil.GetComponent<MeshRenderer>().material = materials.Item2;
                                }
                                tff = !tff;
                            }
                        }
                    }
                }
            }
        }
    }


}
