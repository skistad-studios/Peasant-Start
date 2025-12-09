using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace PeasantStart
{
    internal class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            new Harmony("PeasantStart").PatchAll();
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            if (game.GameType is Campaign)
            {
                CampaignGameStarter campaignStarter = (CampaignGameStarter)gameStarterObject;
                campaignStarter.AddBehavior(new StartingOverrideBehavior());
                campaignStarter.AddBehavior(new VillageWorkBehavior());
                campaignStarter.AddBehavior(new VillageRecruitPeasantBehavior());
            }
        }
    }
}