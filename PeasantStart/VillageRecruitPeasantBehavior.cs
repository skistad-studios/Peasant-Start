using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace PeasantStart
{
    internal class VillageRecruitPeasantBehavior : CampaignBehaviorBase
    {
        private const int HoursToRecruit = 4;
        private const int RecruitCooldown = 8;

        private const float BaseRecruitChance = 0.2f;
        private const float MaxRecruitChance = 0.95f;
        private const float ClanTierRecruitChancePenalty = 0.1f;
        private const int MaxPeasantsPerRecruitment = 6;
        private const int CostToHirePeasant = 10;

        private const float CharmXpPerHour = 4.0f;

        private bool isRecruiting;
        private int hoursRecruited;
        private int recruitStamina;
        private int peasantsRecruited;
        private Settlement lastSettlement;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, this.OnSessionLaunched);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, this.OnHourlyTick);
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, this.OnSettlementEntered);
            CampaignEvents.VillageBeingRaided.AddNonSerializedListener(this, this.OnVillageBeingRaided);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("ps_isRecruiting", ref this.isRecruiting);
            dataStore.SyncData("ps_hoursRecruited", ref this.hoursRecruited);
            dataStore.SyncData("ps_recruitStamina", ref this.recruitStamina);
            dataStore.SyncData("ps_peasantsRecruited", ref this.peasantsRecruited);
            dataStore.SyncData("ps_lastRecruitedSettlement", ref this.lastSettlement);
        }

        internal void StartRecruiting()
        {
            if (Settlement.CurrentSettlement == null)
            {
                return;
            }

            if (this.isRecruiting)
            {
                return;
            }

            if (this.recruitStamina < RecruitCooldown)
            {
                return;
            }

            this.isRecruiting = true;
            this.hoursRecruited = 0;
            this.peasantsRecruited = 0;

            GameMenu.SwitchToMenu("ps_village_recruiting");
        }

        internal void StopRecruiting()
        {
            if (Settlement.CurrentSettlement == null)
            {
                return;
            }

            if (!this.isRecruiting)
            {
                return;
            }

            this.isRecruiting = false;

            if (this.hoursRecruited >= HoursToRecruit)
            {
                this.peasantsRecruited = this.RollPeasantsRecruited();
                this.recruitStamina = 0;
                GameMenu.SwitchToMenu("ps_village_pick_recruits");
            }
            else
            {
                GameMenu.SwitchToMenu("village");
            }
        }

        internal void RecruitHour()
        {
            if (Settlement.CurrentSettlement == null)
            {
                return;
            }

            if (!this.isRecruiting)
            {
                return;
            }

            if (this.hoursRecruited > 0 || (CampaignTime.Now.CurrentHourInDay > Utilities.PeasantWakeUpHour && CampaignTime.Now.CurrentHourInDay <= Utilities.PeasantSleepHour))
            {
                this.hoursRecruited += 1;

                this.GrantRecruitXp(Hero.MainHero);

                if (this.hoursRecruited >= HoursToRecruit)
                {
                    this.StopRecruiting();
                }
            }
        }

        internal void RecruitPeasants(int amount)
        {
            if (Settlement.CurrentSettlement == null)
            {
                return;
            }

            int recruitmentCost = this.GetRecruitmentCost(amount);

            if (Hero.MainHero.Gold < recruitmentCost)
            {
                return;
            }

            CharacterObject peasantCharacter = this.GetPeasantFromCulture(Settlement.CurrentSettlement.Culture);

            if (peasantCharacter == null)
            {
                return;
            }

            PartyBase.MainParty.AddElementToMemberRoster(peasantCharacter, amount);
            GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, Settlement.CurrentSettlement, recruitmentCost);
        }

        internal void GrantRecruitXp(Hero hero)
        {
            hero.AddSkillXp(DefaultSkills.Charm, CharmXpPerHour);
        }

        internal int RollPeasantsRecruited()
        {
            int count = 0;
            float recruitChance = this.GetPeasantRecruitChance();
            for (int i = 0; i < MaxPeasantsPerRecruitment; i += 1)
            {
                if (MBRandom.RandomFloat <= recruitChance)
                {
                    count += 1;
                }
            }

            return count;
        }

        internal float GetPeasantRecruitChance()
        {
            return MBMath.ClampFloat(BaseRecruitChance + (Hero.MainHero.GetSkillValue(DefaultSkills.Charm) * 0.01f) - (Hero.MainHero.Clan.Tier * ClanTierRecruitChancePenalty), BaseRecruitChance, MaxRecruitChance);
        }

        internal CharacterObject GetPeasantFromCulture(CultureObject culture)
        {
            return CharacterObject.FindFirst(character => character.Culture == Settlement.CurrentSettlement.Culture && character.Tier == 0 && character.Occupation == Occupation.Villager && !character.IsHero);
        }

        internal int GetRecruitmentCost(int numberOfPeasants)
        {
            return numberOfPeasants * CostToHirePeasant;
        }

        private void AddMenuOption(CampaignGameStarter campaignGameStarter)
        {
            // Village recruit option
            campaignGameStarter.AddGameMenuOption(
                "village",
                "ps_recruit",
                "{=ps_recruit_peasants}Recruit Peasants",
                (MenuCallbackArgs args) =>
                {
                    bool canRecruit = this.recruitStamina >= RecruitCooldown;

                    if (!canRecruit)
                    {
                        MBTextManager.SetTextVariable("ps_hours_remaining_until_can_recruit", RecruitCooldown - this.recruitStamina);
                        MBTextManager.SetTextVariable("ps_hour_hours", RecruitCooldown - this.recruitStamina > 1 ? "{=ps_hours}hours" : "{=ps_hour}hour");
                        args.Tooltip = new TextObject("{=ps_recruit_cooldown_tooltip}You recently recruited. You can attempt again in {ps_hours_remaining_until_can_recruit} {ps_hour_hours}, or try another village.");
                    }

                    args.IsEnabled = canRecruit;
                    args.optionLeaveType = GameMenuOption.LeaveType.Recruit;
                    return true;
                },
                (MenuCallbackArgs args) =>
                {
                    this.StartRecruiting();
                });

            // Recruiting menu
            campaignGameStarter.AddWaitGameMenu(
                "ps_village_recruiting",
                "{ps_recruiting_description}",
                null,
                (MenuCallbackArgs args) =>
                {
                    this.UpdateRecruitingMenu(args);
                    args.MenuContext.GameMenu.StartWait();
                    return true;
                },
                null,
                (MenuCallbackArgs args, CampaignTime dt) =>
                {
                    this.UpdateRecruitingMenu(args);
                },
                GameMenu.MenuAndOptionType.WaitMenuShowOnlyProgressOption);

            // Stop recruiting option
            campaignGameStarter.AddGameMenuOption(
                "ps_village_recruiting",
                "ps_village_stop_recruiting",
                "{=ps_leave]}Leave",
                (MenuCallbackArgs args) =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                (MenuCallbackArgs args) =>
                {
                    this.StopRecruiting();
                });

            // Pick recruits menu
            campaignGameStarter.AddGameMenu(
                "ps_village_pick_recruits",
                "{ps_pick_recruits_description}",
                (MenuCallbackArgs args) =>
                {
                    if (this.peasantsRecruited > 0)
                    {
                        MBTextManager.SetTextVariable("ps_peasants_recruited", this.peasantsRecruited);
                        MBTextManager.SetTextVariable("ps_peasant_peasants", this.peasantsRecruited > 1 ? "{=ps_peasants}peasants" : "{=ps_peasant}peasant");
                        MBTextManager.SetTextVariable("ps_pick_recruits_description", "{=ps_recruit_success}You managed to convince {ps_peasants_recruited} {ps_peasant_peasants} to join your cause.");
                    }
                    else
                    {
                        MBTextManager.SetTextVariable("ps_pick_recruits_description", "{=ps_recruit_failure}You didn't manage to convince anyone to join your cause.");
                    }

                    args.MenuContext.SetBackgroundMeshName(Settlement.CurrentSettlement.Culture.StringId + "_tavern");
                });

            // Pick recruits menu options
            for (int i = 0; i < MaxPeasantsPerRecruitment; i += 1)
            {
                int n = i;
                int costToHire = this.GetRecruitmentCost(n + 1);
                campaignGameStarter.AddGameMenuOption(
                "ps_village_pick_recruits",
                $"ps_hire_recruits_{n}",
                "{ps_hire_recruits_option_name_" + n + "}",
                (MenuCallbackArgs args) =>
                {
                    string text = new TextObject("{=ps_hire_recruits}Hire [ps_hire_count] [ps_peasant_peasants] for [ps_hiring_cost]{GOLD_ICON}").ToString();
                    text = text.Replace("[ps_hire_count]", (n + 1).ToString()).Replace("[ps_peasant_peasants]", n > 0 ? "{=ps_peasants}peasants" : "{=ps_peasant}peasant").Replace("[ps_hiring_cost]", costToHire.ToString());
                    MBTextManager.SetTextVariable($"ps_hire_recruits_option_name_{n}", text);

                    bool canHire = Hero.MainHero.Gold >= costToHire && PartyBase.MainParty.MemberRoster.TotalManCount + n + 1 <= PartyBase.MainParty.PartySizeLimit;
                    if (!canHire)
                    {
                        if (Hero.MainHero.Gold < costToHire)
                        {
                            args.Tooltip = new TextObject("{=ps_recruit_not_enough_gold_tooltip}You can't affoard this.");
                        }
                        else
                        {
                            args.Tooltip = new TextObject("{=ps_recruit_not_enough_space_tooltip}You don't have enough room in your party.");
                        }
                    }

                    args.IsEnabled = canHire;
                    args.optionLeaveType = GameMenuOption.LeaveType.Recruit;
                    return this.peasantsRecruited > n;
                },
                (MenuCallbackArgs args) =>
                {
                    this.RecruitPeasants(n + 1);
                    GameMenu.SwitchToMenu("village");
                });
            }

            // Leave pick recruits menu option
            campaignGameStarter.AddGameMenuOption(
                "ps_village_pick_recruits",
                "ps_village_leave_pick_recruits",
                "{=ps_leave]}Leave",
                (MenuCallbackArgs args) =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                (MenuCallbackArgs args) =>
                {
                    GameMenu.SwitchToMenu("village");
                });
        }

        private void UpdateRecruitingMenu(MenuCallbackArgs args)
        {
            if (this.hoursRecruited <= 0 && (CampaignTime.Now.CurrentHourInDay < Utilities.PeasantWakeUpHour || CampaignTime.Now.CurrentHourInDay >= Utilities.PeasantSleepHour))
            {
                MBTextManager.SetTextVariable("ps_recruiting_description", "{=ps_recruiting_description_asleep}Everyone is asleep.");
                args.MenuContext.SetBackgroundMeshName(Settlement.CurrentSettlement.SettlementComponent.WaitMeshName);
            }
            else
            {
                MBTextManager.SetTextVariable("ps_recruiting_description", "{=ps_recruiting_description_recruiting}You attempt to convince some local peasants to join you.");
                args.MenuContext.SetBackgroundMeshName(Settlement.CurrentSettlement.Culture.StringId + "_tavern");
            }

            args.MenuContext.GameMenu.SetProgressOfWaitingInMenu(this.hoursRecruited < HoursToRecruit ? (float)this.hoursRecruited / HoursToRecruit : 0);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            this.AddMenuOption(starter);
        }

        private void OnHourlyTick()
        {
            if (isRecruiting)
            {
                this.RecruitHour();
            }
            else if (this.recruitStamina < RecruitCooldown)
            {
                this.recruitStamina += 1;
            }
        }

        private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
        {
            if (this.lastSettlement != Settlement.CurrentSettlement)
            {
                this.recruitStamina = RecruitCooldown;
                this.lastSettlement = Settlement.CurrentSettlement;
            }
        }

        private void OnVillageBeingRaided(Village village)
        {
            if (isRecruiting && village == Settlement.CurrentSettlement.Village)
            {
                this.StopRecruiting();
            }
        }
    }
}