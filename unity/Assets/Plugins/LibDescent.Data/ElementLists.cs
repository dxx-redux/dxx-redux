/*
    Copyright (c) 2019 SaladBadger

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/

namespace LibDescent.Data
{
    class ElementLists
    {
        public static string[] guns = {"Laser level 1", "Laser level 2", "Laser level 3", "Laser level 4",
                            "Blue Energy Ball",
                            "Class 1 Drone Fireball", "Reactor Energy Ball", "Nothing", "Concussion Missile", "Flare",
                            "Robot Blue Laser", "Vulcan Bullet", "Spreadfire Blob", "Plasma Ball", "Fusion Blob",
                            "Homing Missile", "Proximity Bomb", "Smart Missile", "Mega Missile", "Smart Missile Child",
                            "Robot Spreadfire Blob", "Supermech Homing Missile", "Robot Concussion Missile", "Silent Spreadfire Blob",
                            "Robot Red Laser", "Robot Green Laser", "Robot Plasma Ball", "Spider Fireball",
                            "Robot Mega Missile", "Robot Smart Missile Child", "Laser Level 5", "Laser Level 6",
                            "Gauss Bullet", "Helix Blob", "Phoenix Fireball", "Omega Beam", "Flash Missile", 
                            "Guided Missile", "Smart Mine", "Mercury Missile", "Earthshaker Missile", 
                            "Robot Vulcan Bullet", "PEST Blob", "Robot White Laser", "Robot Phoenix Fireball",
                            "Fast Robot Phoenix Fireball", "Robot Helix Blob", "Smart Mine Child",
                            "Sidearm Energy Ball", "Robot Smart Mine Child", "Robot Flash Missile", "Mine",
                            "Robot Gauss Bullet", "Robot Smart Mine", "Earthshaker Tracker", "Robot Mercury Missile",
                            "Fast Smart Mine Child", "Robot Smart Missile", "Robot Earthshaker Missile",
                            "Robot Earthshaker Tracker", "Robot Omega Beam", "Robot Homing Flash Missile"};

        public static string[] vclips = {"Explosion13", "RedSpark", "Explosion20", "ScreenShatter", "Unused4",
                            "MissileExplosion", "Unused6", "Explosion15", "UnusedGetHostage", "BlackSpark",
                            "MatcenCreate", "RedMuzzleFlash", "GreenMuzzleFlash", "Unused13", "BlueMuzzleFlash", "PurpleMuzzleFlash",
                            "Unused16", "Unused17", "EnergyPickup", "ShieldPickup", "LaserCannon",
                            "PurpleImpact", "GreenImpact", "BlueImpact", "BlueKey", "YellowKey",
                            "RedKey", "Unused27", "Unused28", "Unused29", "PurpleSpark",
                            "BlueSpark", "GreenSpark", "Hostage", "ConcussionMissileItem",
                            "ConcussionMissilePackItem", "ExtraLifeItem", "VulcanCannon", "SpreadfireCannon", "PlasmaCannon",
                            "FusionCannon", "ProxbombPackItem", "HomingMissileItem", "HomingMissilePackItem", "SmartMissileItem",
                            "MegaMissileItem", "VulcanAmmoItem", "CloakItem", "Unused48", "InvulnerabilityItem", "Unused50", "QuadLaserItem", "Unused52", "ProxBombProjectile",
                            "PlasmaProjectile", "BlueProjectile", "Class1DroneProjectile", "Unused57",
                            "ExplosionPlayerDeath", "RedImpact", "Explosion19", "PlayerSpawn", "ItemSpawn",
                            "MegaMissileImpact", "ReactorProjectile", "FusionImpact", "BlackSpark2", "HelixProjectileRobot",
                            "PhoenixProjectile", "GaussCannon", "HelixCannon", "PhoenixCannon", "OmegaCannon", "MapItem", "PowerConverter",
                            "AmmoRack", "Afterburner", "SuperLaserCannon", "FlashMissileItem", "GuidedMissileItem", "SmartMineProjectile",
                            "MercuryMissileItem", "EarthshakerMissileItem", "Headlight", "WaterSplash", "YellowMuzzleFlash",
                            "WhiteMuzzleFlash", "YellowSpark", "WhiteSpark", "FlashMissilePackItem", "GuidedMissilePackItem",
                            "SmartMinePackItem", "SmartMineTrackerProjectile", "SwitchImpact", "SwitchImpact2", "GaussImpact", "AfterburnerTrail",
                            "PESTProjectile", "HelixProjectile", "MonitorStatic", "BlueFlag", "RedFlag", "MercuryMissilePackItem",
                            "OmegaProjectile", "EarthshakerMissileImpact", "OmegaImpact", "MercuryExplosion" };

        public static string[] eclips = {"ArrowStrip", "Unused1", "EnergySparkles", "Fan", "Unused4", "Unused5", "Unused6",
                            "Unused7", "Unused8", "Slime", "Unused10", "Unused11", "ReactorRoom1", "ReactorRoom2",
                            "ReactorRoom3", "ReactorRoom4", "MatcenEdge", "Matcen", "Unused18", "LandingPad", "Screen1",
                            "Screen2", "Screen3", "Screen4", "Screen5", "Screen6", "Screen7", "Screen8", "Unused28", "Unused29",
                            "Unused30", "RobotEyes1", "RobotThruster", "Lava1", "Flare", "Screen1Break",
                            "Screen2Break", "Screen3Break", "Screen4Break", "Screen5Break", "Screen6Break", "Screen7Break",
                            "Screen1Crit", "Screen2Crit", "Screen3Crit", "Screen4Crit", "Screen5Crit", "Screen6Crit",
                            "Screen7Crit", "RobotEyes2", "ReactorTex1", "ReactorTex2", "ReactorTex3",
                            "BossGlow", "BossEye", "Water1", "RobotEyes3", "Waterfall1", "Waterfall2", "Water2", "Water3",
                            "Lava2", "Lava3", "Lava4", "Lavafall1", "Lavafall2", "Lava5", "RobotEyes4", "RobotEyes5", "DoorLightBlue",
                            "DoorLightYellow", "DoorLightRed", "Switch1", "Switch2", "Switch3", "Switch1Break", "EnergySparkles2",
                            "ReactorTex4", "ForcefieldDiag", "Waterfall3", "MarkerTexture", "ReactorTex5", "Switch2Break",
                            "Switch3Break", "TechnoStrip1", "TechnoStrip2", "Screen8Break", "SecretPortal", "DoorLightBlue2",
                            "DoorLightYellow2", "DoorLightRed2", "Screen8Crit", "Screen9Crit", "Forcefield", "BlueGoal", "RedGoal",
                            "EBanditLightning", "Screen9", "Screen9Break", "Screen10Crit", "Screen10", "Screen10Break", "Unused102",
                            "Screen11Crit", "BossWeakSpot"};

        //all robots
        public static string[] robots = {"Medium Hulk", "Medium Lifter", "Spider Processer", "Class 1 Drone", "Class 2 Drone",
                    "Cloaked Driller", "Cloaked Medium Hulk", "Supervisor", "Secondary Lifter", "Heavy Driller",
                    "Gopher", "Laser Platform Robot", "Missile Platform Robot", "Splitter Pod", "Baby Spider",
                    "Fusion Hulk", "Supermech", "Level 7 Boss", "Cloaked Lifter", "Class 1 Driller", "Light Hulk",
                    "Advanced Lifter", "Defense Prototype", "Level 27 Boss", "BPER Bot", "Smelter", "Ice Spindle",
                    "Bulk Destroyer", "TRN Racer", "Fox Attack Bot", "Sidearm", "Red Fatty Boss", "New Boss",
                    "Guidebot", "Mine Guard", "Evil Twin", "ITSC Bot", "ITD Bot", "PEST", "PIG", 
                    "Diamond Claw", "Hornet", "Thief Bot", "Seeker", "E-Bandit", "Fire Boss", "Water Boss",
                    "Boarshead", "Spider", "Omega Defense Spawn", "Sidearm Modula", "LOU Guard", "Ailen 1 Boss",
                    "Popcorn Miniboss", "Cloaked Diamond Claw", "Cloaked Smelter", "Guppy", "Smelter Clone",
                    "Omega Defense Spawn Clone", "BPER Bot Clone", "Spider Clone", "Spawn Clone", "Ice Boss", "Spawn",
                    "Final Boss", "Mini Reactor"};

         //powerups
        public static string[] powerups = {"Extra Life", "Energy", "Shield", "Laser Cannon", "Blue Key", "Red Key", "Yellow Key",
                      "Hoard Orb", "Unused (Powerup Revealer)", "Unused (Map Revealer)", "Concussion Missile", "Concussion Pack", "Quad Laser",
                      "Vulcan Cannon", "Spreadfire Cannon", "Plasma Cannon", "Fusion Cannon", "Proximity Bomb", "Homing Missile", "Homing Pack", 
                      "Smart Missile", "Mega Missile", "Vulcan Ammo", "Cloak", "Unused (Turbo)", "Invulnerability",
                      "Unused (Headlight)", "Unused (Mega-Wowie-Zowie!)", "Gauss Cannon", "Helix Cannon", "Phoenix Cannon", "Omega Cannon",
                      "Super Laser Cannon", "Full Map", "Converter", "Ammo Rack", "Afterburner", "Headlight", "Flash Missile",
                      "Flash Pack", "Guided Missile", "Guided Pack", "Smart Mine", "Mercury Missile",
                      "Mercury Pack", "Earthshaker Missile", "Blue Flag", "Red Flag"};

        public static string[] polymodels = {"Medium Hulk", "Medium Hulk LOD", "Medium Lifter", "Medium Lifter LOD", "Spider Processor",
                      "Spider Processor LOD", "Class 1 Drone", "Class 1 Drone LOD", "Class 2 Drone", "Class 2 Drone LOD", "Cloaked Driller",
                      "Cloaked Driller LOD", "Cloaked Medium Hulk", "Cloaked Medium Hulk LOD", "Supervisor", "Secondary Lifter", "Secondary Lifter LOD", 
                      "Heavy Driller", "Heavy Driller LOD", "Gopher", "Laser Platform Robot", "Missile Platform Robot", "Splitter Pod", "Baby Spider", 
                      "Baby Spider LOD", "Fusion Hulk", "Supermech", "Supermech LOD", "Level 7 Boss", "Cloaked Lifter", "Cloaked Lifter LOD",
                      "Class 1 Driller", "Class 1 Driller LOD", "Light Hulk", "Light Hulk LOD", "Advanced Lifter", "Advanced Lifter LOD",
                      "Defense Prototype", "Defense Prototype LOD", "Level 27 Boss", "BPER Bot", "Smelter", "Smelter LOD", "Ice Spindle",
                      "Bulk Destroyer", "TRN Racer", "Fox Attack Bot", "Sidearm", "Sidearm LOD", "Red Fatty Boss", "New Boss", "Guidebot",
                      "Mine Guard", "Mine Guard LOD", "Evil Twin", "ITSC Bot", "ITD Bot", "ITD Bot LOD", "PEST Bot", "PEST LOD", 
                      "PIG", "PIG Bot LOD", "Diamond Claw", "Diamond Claw LOD", "Hornet", "Thief Bot", "Thief Bot (LD)", "Seeker",
                      "E-Bandit", "Fire Boss", "Water Boss", "Boarshead", "Spider", "Omega Defense Spawn", "Sidearm Modula", "LOU Guard",
                      "Alien 1 Boss", "Popcorn Miniboss", "Cloaked Diamond Claw", "Cloaked Diamond Claw LOD", "Cloaked Smelter", "Cloaked Smelter LOD",
                      "Guppy", "Smelter Clone", "Smelter Clone LOD", "Omega Defense Spawn Clone", "BPER Bot Clone", "Spider Clone", "Spawn", "Ice Boss",
                      "Spawn Clone", "Final Boss", "Mini Reactor", "Descent 1 Reactor", "Descent 1 Reactor Destroyed", "Alien Reactor", "Ailen Reactor Destroyed", 
                      "Zeta Aquilae Reactor", "Zeta Aquilae Reactor Destroyed", "Water Reactor", "Water Reactor Destroyed", "Ailen 1 Reactor",
                      "Ailen 1 Reactor Destroyed", "Fire Reactor", "Fire Reactor Destroyed", "Ice Reactor", "Ice Reactor Destroyed", "Marker", "Pyro GX",
                      "Pyro GX LOD", "Pyro GX Debris", "Red Laser", "Red Laser LOD", "Red Laser LOD 2", "Red Laser Core", "Purple Laser", "Purple Laser LOD",
                      "Purple Laser LOD 2", "Purple Laser Core", "Light Blue Laser", "Light Blue Laser LOD", "Light Blue Laser LOD2", "Light Blue Laser Core", "Green Laser",
                      "Green Laser LOD", "Green Laser LOD 2", "Green Laser Core", "Concussion Missile", "Flare", "Robot Blue Laser", "Robot Blue Laser Core",
                      "Fusion Blob", "Fusion Blob Core", "Homing Missile", "Smart Missile", "Mega Missile", "Robot Homing Missile", "Robot Concussion Missile", "Robot Red Laser", 
                      "Robot Red Laser Core", "Robot Green Laser", "Robot Green Laser Core", "Robot Mega Missile", "Yellow Laser", "Yellow Laser LOD", "Yellow Laser LOD 2", "Yellow Laser Core", 
                      "White Laser", "White Laser LOD", "White Laser LOD 2", "White Laser Core", "Flash Missile", "Guided Missile", "Mercury Missile", "Earthshaker Missile",
                      "Robot Vulcan", "Robot White Laser", "Robot White Laser Core", "Robot Flash Missile", "Mine", "Earthshaker Child", "Robot Mercury Missile", "Robot Smart Missile", 
                      "Robot Earthshaker Missile", "Robot Earthshaker Missile Child", "Robot Homing Flash Missile"};

        public static string[] polymodelsDemo = { "Smelter", "Smelter LOD", "Sidearm", "Sidearm LOD", "Red Fatty", "Red Fatty LOD",
            "Guidebot", "Guidebot LOD", "ITD", "ITD LOD", "PEST", "PEST LOD", "PIG", "PIG LOD", "Diamond Claw", "Diamond Claw LOD",
            "Thief Bot", "Thief Bot LOD", "Sidearm Modula", "Sidearm Modula LOD", "Zeta Aquilae Reactor", "Zeta Aquilae Reactor Destroyed",
            "Marker", "Pyro-GX", "Pyro-GX LOD", "Pyro-GX Debris", "Mine Exit", "Mine Exit Destroyed", "Red Laser", "Red Laser LOD",
            "Red Laser LOD 2", "Red Laser Core", "Purple Laser", "Purple Laser LOD", "Purple Laser LOD 2", "Purple Laser Core",
            "Light Blue Laser", "Light Blue Laser LOD", "Light Blue Laser LOD2", "Light Blue Laser Core", "Green Laser", "Green Laser LOD",
            "Green Laser LOD 2", "Green Laser Core", "Concussion Missile", "Flare", "Robot Blue Laser", "Robot Blue Laser Core",
            "Homing Missile", "Smart Missile", "Robot Homing Missile", "Robot Concussion Missile", "Robot Red Laser", "Robot Red Laser Core",
            "Robot Green Laser", "Robot Green Laser Core", "Yellow Laser", "Yellow Laser LOD", "Yellow Laser LOD 2", "Yellow Laser Core", "White Laser",
            "White Laser LOD", "White Laser LOD 2", "White Laser Core", "Flash Missile", "Guided Missile", "Robot Vulcan", "Robot White Laser",
            "Robot White Laser Core", "Robot Flash Missile", "Mine" };

        public static string[] sounds = {"Silence", "Seeker sight", "Seeker attack", "BPER sight", "BPER attack", "Boarshed sight", "Boarshed attack",
                      "TRN Racer sight", "TRN Racer attack", "Bulk Destroyer sight", "Unused laser fire", "Explosion", "Smart Blob launch",
                      "Laser level 1 fire", "Laser level 2 fire", "Laser level 3 fire", "Laser level 4 fire", "Player hit robot", "Spreadfire fire",
                      "Class 1 drone fire", "Robot death explosion", "Robot hit explosion", "Reactor fire", "Flare fire", "Fusion fire",
                      "Plasma fire", "Mine laid", "Invulnerable hit", "Wall hit", "Thief death", "Reactor hit", "Player explode",
                      "Siren", "Mine explode", "Fusion charge", "Bulk Destroyer fire", "Hornet sight", "Helix fire", "Phoenix fire",
                      "Weapon dropped", "Forcefield touched", "Forcefield shot", "Forcefield loop", "Forcefield disabled", "Laser level 5 fire", "Laser level 6 fire",
                      "Hornet attack", "Ice Spindle sight", "Ice Spindle attack", "Omega sight", "Marker beep", "Guidebot goal found", "Guidebot death", "Omega attack",
                      "ITSC sight", "ITSC attack", "Enemy explode 1", "Enemy explode 2", "Enemy explode 3", "ITD sight", "ITD attack", "Fox Attack Bot sight",
                      "Energy center charge", "Fox Attack Bot attack", "EBandit sight", "EBandit attack", "Lou Guard sight", "Lou Guard attack", "Evil Twin see", "Evil Twin attack",
                      "Player hit wall", "Player damaged", "Spider sight", "Spider attack", "Guppy sight", "Guppy attack", "Flash missile", "Omega fire", "Unused #78",
                      "Unused #79", "Shield powerup", "Energy powerup", "Extra Life", "Item pickup", "Unused #84", "Unused #85", "Unused #86", "Unused #87",
                      "Unused #88", "Unused #89", "Unused #90", "Hostage rescued", "Spawn", "Despawn", "Briefing hum", "Briefing typing", "Unused #96", "Unused #97",
                      "Unused #98", "Unused #99", "Countdown 0", "Countdown 1", "Countdown 2", "Countdown 3", "Countdown 4", "Countdown 5", "Countdown 6",
                      "Countdown 7", "Countdown 8", "Countdown 9", "Unused #110", "Unused #111", "Unused #112", "Countdown 10", "Countdown start", "Vulcan fire", "Unused #116",
                      "Message recieved", "Other player died", "Unused #119", "Unused #120", "Fan swish", "Lock on alarm", "Join request", "Blue got flag", "Red got flag", "You got flag",
                      "Blue scored", "Red scored", "You scored", "Missile launch", "Unused #131", "Mega Missile launch", "Mercury Missile launch", "Unused #134", "Unused #135",
                      "Unused #136", "Unused #137", "Unused #138", "Unused #139", "Door 1 open", "Door 1 close",
                      "Door 2 open", "Door 2 close", "Door 3 open", "Door 3 close", "Door 4 open", "Door 4 close", "Door 5 open", "Door 5 close", "In lava fall", "In lava", "In waterfall",
                      "Change primary", "Change secondary", "Weapon already selected", "Don't have weapon", "Monitor shot", "In water", "Unused #159", "Cloak pickup",
                      "Cloak wore off", "Invulnerability pickup", "Invulnability wore off", "Unused #164", "Inused #165", "Unused #166", "Guidebot sight", "Sidearm Modula sight", "Driller sight",
                      "Class 1 Drone sight", "Thief sight", "Medium Hulk sight", "Light Hulk sight", "Unused #174", "Unused #175", "Supermech sight", "Unused #177",
                      "Diamond Claw sight", "Unused #179", "PIG sight", "Baby Spider sight", "Unused #182", "Boss 1 loop", "Boss 1 attack", "Old boss death",
                      "Boss 2 loop", "Boss 2 attack", "Boss 1 death", "Unused #189", "Claw tear", "Unused #191", "Boss 5 loop", "Boss 5 attack", "Boss 5 death",
                      "Boss 6 loop", "Boss 6 attack", "Boss 6 death", "Unused #198", "Unused #199", "Cheater!", "Supervisor sight",
                      "Sidearm sight", "Cloaked Driller attack", "Laser Platform sight", "Defense Prototype sight", "Unused #206", "Unused #207", "Unused #208", "Secondary Lifter sight",
                      "Unused #210", "Smelter sight", "Unused #212", "Unused #213", "Unused #214", "Unused #215", "Unused #216", "Unused #217", "Unused #218", "PEST sight", "Popcorn sight",
                      "Secret exit", "Lava bubbling", "Water splashing", "Lava flow", "Boss 3 loop", "Boss 3 attack", "Boss 3 death", "Unused #228", "Unused #229", "Gauss fire",
                      "Mega Missile explosion", "Water shot", "Water splash", "Unused #234", "Waterfall", "Boss 4 loop", "Boss 4 attack", "Boss 4 death", "Unused #239", "Switch hit",
                      "Converter", "E-Bandit drain", "Unused #243", "Thief steal", "Earthshaker Missile explode", "Illusionary Wall disabled", "Afterburner", "Afterburner end",
                      "Secret warp", "Earthshaker launch", "Earthquake", "Unsued #252", "Unused #253"};

        public static string[] reactors = { "Descent 1 Reactor", "Alien 2 Reactor", "Zeta Aquilae Reactor", "Water Reactor", "Fire Reactor", "Alien 1 Reactor", "Ice Reactor" };

        public static string GetWeaponName(int weapon_type)
        {
            if (weapon_type >= ElementLists.guns.Length)
            {
                return "New weapon " + weapon_type.ToString();
            }
            return ElementLists.guns[weapon_type] + " #" + weapon_type.ToString();
        }

        public static string GetVClipName(int vclip)
        {
            if (vclip >= ElementLists.vclips.Length)
            {
                return "NewVClip " + vclip.ToString();
            }
            return ElementLists.vclips[vclip] + " #" + vclip.ToString();
        }
        public static string GetEClipName(int eclip)
        {
            if (eclip >= ElementLists.eclips.Length)
            {
                return "NewEClip" + eclip.ToString();
            }
            return ElementLists.eclips[eclip];
        }
        public static string GetRobotName(int robot_type)
        {
            if (robot_type >= ElementLists.robots.Length)
            {
                return "New robot " + robot_type.ToString();
            }
            return ElementLists.robots[robot_type] + " #" + robot_type.ToString();
        }
        public static string GetPowerupName(int powerup)
        {
            if (powerup >= ElementLists.powerups.Length)
            {
                return "New Powerup " + powerup.ToString();
            }
            return ElementLists.powerups[powerup];
        }
        public static string GetModelName(int model)
        {
            if (model >= ElementLists.polymodels.Length)
            {
                return "New Model " + model.ToString();
            }
            return ElementLists.polymodels[model];
        }
        public static string GetDemoModelName(int model)
        {
            if (model >= ElementLists.polymodelsDemo.Length)
            {
                return "New Model " + model.ToString();
            }
            return ElementLists.polymodelsDemo[model];
        }
        public static string GetSoundName(int soundID)
        {
            if (soundID >= sounds.Length)
            {
                return "New sound " + soundID.ToString();
            }
            return ElementLists.sounds[soundID];
        }
        public static string GetReactorName(int reactor)
        {
            if (reactor >= reactors.Length)
                return "New Reactor " + reactor.ToString();
            return reactors[reactor];
        }
    }
}
