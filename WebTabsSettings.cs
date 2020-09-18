using UnityEngine;
using Landfall.TABS;
using System.Net;
using UModLoader;
using System.Collections.Generic;

namespace WebTabs
{
    public static class WebTabsSettings
    {
        public static readonly bool printData = false;
        public static readonly bool useDevServer = false;
        public static readonly bool doStepTest = false;
        public static readonly bool propStandIn = false;
        public static readonly string serverURL = (!useDevServer ? @"https://webtabs.tk/upload/" : @"http://localhost/webtabs/upload/");
        public static readonly string clientURL = @"http://localhost:7427/";

        public static readonly WebClient webClient = new WebClient();
        public static readonly LandfallUnitDatabase database = LandfallUnitDatabase.GetDatabase();
        public static readonly UPool myPool = UPool.MyPool;

        public static readonly List<string> particleWeapons = new List<string> 
        {
            "Throw_Thunderbolt",
            "Lasso",
            "PriestStaff",
            "Thrown_CandleFire",
            "Leg_WizardStaff"
        };
        public static readonly Dictionary<string, string> legacyConversion = new Dictionary<string, string>
        {
            {"Sword", "Sword_Squire"},
            {"7_E_CactusDmg", "7_E_CactusHandDmg"},
            {"E_Cactus", "E_Cactus_Spikedmg"}, 
            {"Raptorshieldtest", "ShieldRaptorRider"},
            {"HoboHair001", "TribalHair002"},
            {"NinjaShoes001", "Asia_Shoes002"}
        };
    }
}
