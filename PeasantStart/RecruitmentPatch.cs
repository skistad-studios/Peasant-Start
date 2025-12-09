using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Localization;

namespace PeasantStart
{
    [HarmonyPatch(typeof(PlayerTownVisitCampaignBehavior))]
    internal static class RecruitmentPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("game_menu_town_recruit_troops_on_condition")]
        private static bool game_menu_town_recruit_troops_on_condition_postfix(bool __result, MenuCallbackArgs args)
        {
            return CanRecruit(__result, args);
        }

        [HarmonyPostfix]
        [HarmonyPatch("game_menu_recruit_volunteers_on_condition")]
        private static bool game_menu_recruit_volunteers_on_condition_postfix(bool __result, MenuCallbackArgs args)
        {
            return CanRecruit(__result, args);
        }

        internal static bool CanRecruit(bool originalResult, MenuCallbackArgs args)
        {
            if (Hero.MainHero.Clan.Tier >= 1)
            {
                return originalResult;
            }
            else
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=ps_cant_recruit_tooltip}You need to reach clan tier 1 to recruit troops.");
                return originalResult;
            }
        }
    }
}