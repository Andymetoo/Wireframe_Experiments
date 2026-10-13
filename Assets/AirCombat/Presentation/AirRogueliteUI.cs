using System;
using UnityEngine;

namespace Meridian
{
    /// <summary>The operation shell owns progression; sorties still use AirMission and its renderer.</summary>
    public sealed partial class AirCombatGame
    {
        public RogueRun Campaign { get; private set; }
        float abilityFeedbackTime;
        bool RogueScreenOpen => Campaign!=null&&Campaign.Phase!=RoguePhase.Flying&&!AftermathActive;

        void OpenNewRogueFromSeed()
        {
            int seed;
            if(!int.TryParse(SeedText,out seed)){seedError="Use a whole-number run seed.";return;}
            StartRogueRun(seed);
        }
        public void StartRogueRun(int seed)
        {
            Campaign?.Abandon();
            Campaign=new RogueRun(seed);SeedText=seed.ToString();seedError="";
            Briefing=false;Paused=false;help=false;stickBindings=false;editingSeed=false;menuIndex=0;accumulator=0;
            // The operations table overlooks a fresh, frozen airfield; it never advances a sortie.
            Mission=new AirMission(seed,MissionKind.Duel);View.Build(Mission);
            ResetFlightPresentation();abilityFeedbackTime=0;inputAdapter.Reset();SetCursor();
        }
        public bool LaunchRogueRoute(int index)
        {
            if(Campaign==null||AftermathActive)return false;
            AirMission sortie=Campaign.LaunchRoute(index);if(sortie==null)return false;
            Mission=sortie;View.Build(Mission);Briefing=false;Paused=false;help=false;stickBindings=false;
            accumulator=0;previousHits=0;hitFlash=0;abilityFeedbackTime=0;menuIndex=0;ShowLead=true;ResetFlightPresentation();
            inputAdapter.Reset();SetCursor();return true;
        }
        public bool ChooseRogueReward(int index,int replaceAbilitySlot=1)
        {
            if(Campaign==null||AftermathActive||!Campaign.ChooseReward(index,replaceAbilitySlot))return false;
            menuIndex=0;SetCursor();return true;
        }
        public void ShowStandardMissions()
        {
            Campaign?.Abandon();Campaign=null;
            int seed;if(!int.TryParse(SeedText,out seed))seed=1944;
            StartSortie(seed,selectedKind);ShowBriefing();
        }
        void RestartFlightMode()
        {if(Campaign!=null)StartRogueRun(Campaign.Seed);else StartSortie(Mission.Seed,Mission.Kind);}
        void StepFlight(FlightInput input,float dt)
        {
            if(Campaign==null){Mission.Step(input,dt);ObserveOutcome();return;}
            bool wasFlying=Campaign.Phase==RoguePhase.Flying;
            Campaign.Step(input,dt);
            if(wasFlying&&Campaign.Phase!=RoguePhase.Flying)ObserveOutcome();
        }
        public void StepRogueBotsForDiagnostics(float dt)
        {
            if(Campaign==null)return;
            bool wasFlying=Campaign.Phase==RoguePhase.Flying;
            Campaign.StepBots(dt);View.Sync(Mission,dt);
            if(wasFlying&&Campaign.Phase!=RoguePhase.Flying)ObserveOutcome();
        }
        void CycleAbility(int slot)
        {
            if(Campaign==null||Campaign.OwnedAbilities.Count<2)return;
            int current=-1;
            for(int i=0;i<Campaign.OwnedAbilities.Count;i++)if(Campaign.OwnedAbilities[i]==Campaign.Equipped[slot])current=i;
            Campaign.EquipAbility(slot,Campaign.OwnedAbilities[(current+1)%Campaign.OwnedAbilities.Count]);
        }
        void ServiceWing(int slot)
        {if(Campaign.WingmanHull[slot]<=0)Campaign.RecruitWingman(slot);else Campaign.RepairWingman(slot);}
        void ActivateRogueAbility(int slot)
        { Campaign.ActivateAbility(slot); abilityFeedbackTime=4; }
        void HandleRogueMenu(AirControlIntent intent)
        {
            if(intent.Back){ShowStandardMissions();return;}
            if(editingSeed&&intent.Submit){OpenNewRogueFromSeed();return;}
            int count=Campaign.Phase==RoguePhase.Planning?12:Campaign.Phase==RoguePhase.Reward?Campaign.Rewards.Count+2:4;
            if(intent.MenuMove!=0&&!editingSeed)menuIndex=(menuIndex+intent.MenuMove+count)%count;
            if(!editingSeed&&intent.MissionChoice>=0)
            {
                if(Campaign.Phase==RoguePhase.Planning&&intent.MissionChoice<3){LaunchRogueRoute(intent.MissionChoice);return;}
                if(Campaign.Phase==RoguePhase.Reward&&intent.MissionChoice<Campaign.Rewards.Count){ChooseRogueReward(intent.MissionChoice);return;}
            }
            if(!intent.Submit)return;
            if(Campaign.Phase==RoguePhase.Planning)
            {
                if(menuIndex<3)LaunchRogueRoute(menuIndex);
                else if(menuIndex==3)Campaign.RepairHull();
                else if(menuIndex<6)ServiceWing(menuIndex-4);
                else if(menuIndex==6)Campaign.BuyRefit();
                else if(menuIndex<9)CycleAbility(menuIndex-7);
                else if(menuIndex==9)ShowStandardMissions();
                else if(menuIndex==10)VectorCabinet.GameCollection.ReturnToMenu();
                else OpenNewRogueFromSeed();
            }
            else if(Campaign.Phase==RoguePhase.Reward)
            {
                if(menuIndex<Campaign.Rewards.Count)ChooseRogueReward(menuIndex);
                else if(menuIndex==Campaign.Rewards.Count)ShowStandardMissions();
                else VectorCabinet.GameCollection.ReturnToMenu();
            }
            else EndRunAction(menuIndex);
        }
        void EndRunAction(int index)
        {
            if(index==0)StartRogueRun(Campaign.Seed);
            else if(index==1)StartRogueRun(unchecked(Campaign.Seed+7919));
            else if(index==2)ShowStandardMissions();
            else VectorCabinet.GameCollection.ReturnToMenu();
        }
        bool RunButton(float x,float y,float w,string text,int index,bool enabled=true)
        {
            bool before=GUI.enabled;GUI.enabled=before&&enabled;
            bool result=Button(x,y,w,(menuIndex==index?"> ":"")+text,menuIndex==index);
            GUI.enabled=before;return result;
        }
        void RogueScreen()
        {
            Fill(0,0,1280,800,new Color(.014f,.027f,.025f,.90f));Panel(24,24,1232,752);
            Label(53,37,830,22,"MERIDIAN 1944  /  "+Campaign.StageName.ToUpperInvariant()+"  /  NO PERSISTENT SAVE",small,amber);
            Label(49,65,850,58,"OPERATION AFTERBURN",title);
            Label(53,120,1170,41,Campaign.LastMessage,description);
            if(Campaign.Phase==RoguePhase.Planning)RoguePlanning();
            else if(Campaign.Phase==RoguePhase.Reward)RogueRewards();
            else RogueEnding();
        }
        void RoguePlanning()
        {
            GUI.SetNextControlName("AfterburnSeed");
            SeedText=GUI.TextField(new Rect(934,54,118,32),SeedText,12);
            editingSeed=GUI.GetNameOfFocusedControl()=="AfterburnSeed";
            if(RunButton(1070,49,154,"NEW RUN",11)){OpenNewRogueFromSeed();return;}
            Label(934,91,290,25,seedError.Length>0?seedError:"RUN SEED  /  replaces this operation",small);
            Panel(49,164,354,448);
            Label(65,175,324,33,Campaign.BuildIdentity,number);
            Label(65,214,324,32,$"HULL {Campaign.Hull:0}%     SUPPLIES {Campaign.Supplies}",body,Campaign.Hull<45?amber:green);
            var d=Campaign.AircraftPreview;var stock=AircraftDefinition.AlliedFighter;
            Label(65,251,320,67,$"Mass {d.Mass:0} kg   Power {d.MaxPower/stock.MaxPower*100:0}%\nWing {d.WingArea:0.0} m²   Roll {d.MaxRollRate/stock.MaxRollRate*100:0}%\nGun damage {Campaign.DamageMultiplier*100:0}%   Rate {Campaign.FireRateMultiplier*100:0}%",description);
            Label(65,320,320,23,"INSTALLED FIELD KITS",small,amber);
            if(Campaign.Mods.Count==0)Label(65,357,320,82,"An ordinary fighter. Salvage and refits will change how it flies and fights.",description);
            for(int i=0;i<Campaign.Mods.Count;i++)
                Label(65,345+i*22,320,26,Campaign.Mods[i].Name+"  Mk "+Campaign.Mods[i].Level,description);
            if(RunButton(65,461,320,"REPAIR +35 HULL / 2 SUPPLIES",3,Campaign.Hull<100&&Campaign.Supplies>=2))Campaign.RepairHull();
            for(int i=0;i<2;i++)
            {
                bool lost=Campaign.WingmanHull[i]<=0;
                string name=i==0?"ROOK":"FINCH";
                string text=lost?name+" LOST / RECRUIT 3":name+" "+Campaign.WingmanHull[i].ToString("0")+"% / REPAIR 1";
                if(RunButton(65,510+i*47,320,text,4+i,Campaign.Supplies>=(lost?3:1)&&(lost||Campaign.WingmanHull[i]<100)))ServiceWing(i);
            }
            for(int i=0;i<Campaign.Routes.Count;i++)
            {
                RogueRoute route=Campaign.Routes[i];float x=425+i*267;
                Panel(x,164,252,296);
                Label(x+13,175,227,23,"SORTIE "+(Campaign.Stage+1)+" / 4  •  ROUTE "+(i+1),small,amber);
                Label(x+13,207,227,57,route.Title,number);
                Label(x+13,266,227,124,route.Description,description);
                Label(x+13,392,227,23,"RISK "+route.Danger+"   /   +"+route.SupplyReward+" SUPPLIES",small,green);
                if(RunButton(x+12,417,228,"FLY ASSIGNMENT ["+(i+1)+"]",i)){LaunchRogueRoute(i);return;}
            }
            Panel(425,481,786,144);
            RogueReward refit=Campaign.RefitOffer;
            Label(442,488,746,28,refit==null?"FIELD WORKSHOP / OPENS AFTER FIRST SORTIE":refit.Title,body,amber);
            Label(442,522,742,51,refit==null?"Bring salvage home. Each later stop offers one fixed refit: spend three crates on equipment or save them for hull and replacement pilots.":RogueCatalog.ModDescription(refit.Mod),description);
            if(RunButton(443,577,346,Campaign.RefitPurchased?"REFIT INSTALLED":"INSTALL REFIT / 3 SUPPLIES",6,refit!=null&&!Campaign.RefitPurchased&&Campaign.Supplies>=3))Campaign.BuyRefit();
            Label(803,575,386,49,"Fuel, ammunition and ability charges rearm free. Hull and pilot losses persist.",small);
            Label(442,625,746,18,"LOADOUT / select a slot to cycle owned abilities",small);
            for(int slot=0;slot<2;slot++)
            {
                float x=425+slot*401;RogueAbility ability=Campaign.Equipped[slot];
                if(RunButton(x,643,385,"["+(slot==0?"T / 5":"Y / 6")+"]  "+RogueCatalog.AbilityName(ability),7+slot))CycleAbility(slot);
                Label(x+8,693,372,69,RogueCatalog.AbilityDescription(ability),description);
            }
            if(RunButton(49,632,354,"END RUN / STANDARD MISSIONS",9)){ShowStandardMissions();return;}
            if(RunButton(49,690,354,"END RUN / CABINET",10)){VectorCabinet.GameCollection.ReturnToMenu();return;}
        }
        void RogueRewards()
        {
            editingSeed=false;
            Label(54,166,1160,34,Campaign.Summary,number,green);
            Label(54,209,1160,30,$"Hull {Campaign.Hull:0}%    Supplies {Campaign.Supplies}    Build: {Campaign.BuildIdentity}    Choose ONE reward.",body);
            for(int i=0;i<Campaign.Rewards.Count;i++)
            {
                RogueReward reward=Campaign.Rewards[i];float x=52+i*299;
                Panel(x,264,279,321);
                Label(x+16,278,249,23,reward.Kind==RogueRewardKind.Modification?"PERMANENT FOR THIS RUN":reward.Kind==RogueRewardKind.Ability?"TACTICAL ABILITY":"GROUND CREW",small,amber);
                Label(x+16,319,249,67,reward.Title,number);
                Label(x+16,385,249,133,reward.Description,description);
                if(RunButton(x+16,533,247,"CLAIM ["+(i+1)+"]",i)){ChooseRogueReward(i);return;}
            }
            Label(54,600,1160,66,"Every choice changes the next flight. Field kits stack up to Mk II. New abilities fill the empty slot, then replace slot 2; all owned abilities remain available in the hangar. Repairs compete with equipment, but fuel and ammunition are always replenished.",body);
            if(RunButton(54,696,560,"END RUN / STANDARD MISSIONS",Campaign.Rewards.Count)){ShowStandardMissions();return;}
            if(RunButton(642,696,578,"END RUN / CABINET",Campaign.Rewards.Count+1)){VectorCabinet.GameCollection.ReturnToMenu();return;}
        }
        void RogueEnding()
        {
            editingSeed=false;
            bool won=Campaign.Phase==RoguePhase.Victory;
            Label(58,187,1120,64,won?"BLOCKADE BROKEN":"OPERATION LOST",title,won?green:amber);
            Label(60,271,1120,80,Campaign.Summary,number);
            Label(60,374,1120,121,$"{Campaign.SortiesWon} / 4 sorties won     {Campaign.TotalKills} aircraft destroyed     {Campaign.TotalHits} confirmed hits\n{(int)Campaign.FlightTime/60:00}:{(int)Campaign.FlightTime%60:00} in the air     {Campaign.TotalRounds} rounds fired     Hull home: {Campaign.Hull:0}%\nRun seed {Campaign.Seed}     Final build: {Campaign.BuildIdentity}",body);
            Label(60,514,1120,67,"No permanent stat grind. A new run starts with the standard fighter, fresh pilots and a different set of choices. The same seed and decisions reproduce the operation.",body);
            if(RunButton(60,617,547,"RETRY THIS RUN",0)){EndRunAction(0);return;}
            if(RunButton(636,617,580,"NEW OPERATION / NEW SEED",1)){EndRunAction(1);return;}
            if(RunButton(60,686,547,"STANDARD MISSIONS",2)){EndRunAction(2);return;}
            if(RunButton(636,686,580,"RETURN TO CABINET",3)){EndRunAction(3);return;}
        }
        void RogueFlightHud()
        {
            for(int slot=0;slot<2;slot++)
            {
                RogueAbility ability=Campaign.Equipped[slot];float x=22,y=368+slot*77;
                Fill(x,y,292,69,new Color(.015f,.028f,.025f,.78f));
                Color tint=Campaign.ActiveSeconds[slot]>0?green:Campaign.Charges[slot]>0?ink:amber;
                Label(x+8,y+4,279,25,(slot==0?"[T]":"[Y]")+"  "+RogueCatalog.AbilityName(ability),description,tint);
                string status=Campaign.ActiveSeconds[slot]>0?"ACTIVE "+Campaign.ActiveSeconds[slot].ToString("0.0")+"s":Campaign.Charges[slot]>0?"READY":"EMPTY";
                if(Campaign.ActiveSeconds[slot]<=0&&ability==RogueAbility.Overboost&&Mission.Player.Health<=15)status="BLOCKED: HULL <=15";
                if(Campaign.ActiveSeconds[slot]<=0&&ability==RogueAbility.Rally&&Mission.WingmenAlive==0)status="NO WINGMEN";
                Label(x+8,y+29,276,20,status+"  /  "+Campaign.Charges[slot]+" USES",small,tint);
                Label(x+8,y+48,279,20,"KEY "+(slot==0?"T or 5":"Y or 6")+"   |   PAD: Y + "+(slot==0?"LB":"RB"),small);
            }
            WorldLabel(22,530,330,52,"AFTERBURN  /  SORTIE "+(Campaign.Stage+1)+" OF 4\n"+Campaign.BuildIdentity,small,green);
            if(!AftermathActive&&(abilityFeedbackTime>0||Campaign.ActiveSeconds[0]>0||Campaign.ActiveSeconds[1]>0))
                WorldLabel(363,596,644,31,Campaign.LastMessage,description,green);
        }
    }
}
