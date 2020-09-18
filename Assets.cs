using System;
using System.Collections.Generic;
using System.Text;
using Landfall.TABS;
using UModLoader;
using UnityEngine;
using System.IO;

namespace WebTabs
{
    public static class Assets
    {
        public static void initialize()
        {
            AssetBundle.LoadFromMemory(Convert.FromBase64String(DataContainer.scene));
/*
            LandfallUnitDatabase db = WebTabsSettings.database;
            UnitBlueprint centurion = ((UnitBlueprint)ULoader.VDic["blueprints"]["Chariot"]).Riders[1];
            centurion.Entity.Name = "Centurion";
            centurion.Entity.GUID = DatabaseID.NewID();
            db.Units.Add(centurion);   
            
            var gladius = centurion.RightWeapon;
            var gladiusWep = gladius.GetComponent<WeaponItem>();
            gladiusWep.Entity.Name = "Gladius";
            gladiusWep.Entity.GUID = DatabaseID.NewID();
            db.Weapons.Add(gladius);

            var shield = centurion.LeftWeapon;
            var shieldWep = shield.GetComponent<WeaponItem>();
            shieldWep.Entity.Name = "Shield_Roman";
            shieldWep.Entity.GUID = DatabaseID.NewID();
            db.Weapons.Add(shield);
            
            string[] npn = new string[] {"Centurion_Helmet","Centurion"}
            int iterator = 0;
            foreach(GameObject prop in centurion.m_props)
            {
                var propWep = prop.GetComponent<CharacterItem>();
                if(propWep)
                {
                    propWep.Entity.Name = prop.name;
                    propWep.Entity.GUID = DatabaseID.NewID();
                    db.CharacterProps.Add(prop);
                }
            }
            ULoader.GenDicts();
            */
        }
    }
}
