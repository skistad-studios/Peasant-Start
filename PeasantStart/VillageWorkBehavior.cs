using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace PeasantStart
{
    internal class VillageWorkBehavior : CampaignBehaviorBase
    {
        private const string WorkingDescriptionId = "ps_working_description";

        private const int HoursInShift = 8;

        private const int Tier0Wage = 0;
        private const int Tier1Wage = 3;
        private const int Tier2Wage = 6;
        private const int Tier3Wage = 9;

        private const float Tier0WageWeight = 0.05f;
        private const float Tier1WageWeight = 0.35f;
        private const float Tier2WageWeight = 0.5f;
        private const float Tier3WageWeight = 0.1f;

        private const float AthleticsXpPerHour = 4.0f;

        private bool isWorking;
        private int hoursWorked;
        private Settlement lastWorkedSettlement;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, this.OnSessionLaunched);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, this.OnHourlyTick);
            CampaignEvents.VillageBeingRaided.AddNonSerializedListener(this, this.OnVillageBeingRaided);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("ps_isWorking", ref this.isWorking);
            dataStore.SyncData("ps_hoursWorked", ref this.hoursWorked);
            dataStore.SyncData("ps_lastWorkedSettlement", ref this.lastWorkedSettlement);
        }

        internal void StartWorking()
        {
            if (Settlement.CurrentSettlement == null)
            {
                return;
            }

            if (this.isWorking)
            {
                return;
            }

            this.isWorking = true;

            if (Settlement.CurrentSettlement != this.lastWorkedSettlement && this.hoursWorked < HoursInShift)
            {
                this.hoursWorked = 0;
            }

            this.lastWorkedSettlement = Settlement.CurrentSettlement;

            GameMenu.SwitchToMenu("ps_village_working");
        }

        internal void StopWorking()
        {
            if (Settlement.CurrentSettlement == null)
            {
                return;
            }

            if (!this.isWorking)
            {
                return;
            }

            this.isWorking = false;

            GameMenu.SwitchToMenu("village");
        }

        internal void DoWorkHour()
        {
            if (Settlement.CurrentSettlement == null)
            {
                return;
            }

            if (!this.isWorking)
            {
                return;
            }

            if (CampaignTime.Now.CurrentHourInDay > Utilities.PeasantWorkHour && CampaignTime.Now.CurrentHourInDay <= Utilities.PeasantLeisureHour && this.hoursWorked < HoursInShift)
            {
                this.hoursWorked += 1;

                foreach (TroopRosterElement member in PartyBase.MainParty.MemberRoster.GetTroopRoster())
                {
                    if (member.Character.IsHero)
                    {
                        if (member.Character.HeroObject == Hero.MainHero || !member.Character.HeroObject.IsWounded)
                        {
                            this.GrantWorkXp(member.Character.HeroObject);
                        }
                    }
                }

                if (this.hoursWorked >= HoursInShift)
                {
                    this.PayWages();
                }
            }
        }

        internal void PayWages()
        {
            if (Settlement.CurrentSettlement == null)
            {
                return;
            }

            int goldEarned = 0;
            int tier0WagesPaid = 0;
            int tier1WagesPaid = 0;
            int tier2WagesPaid = 0;
            int tier3WagesPaid = 0;
            int villageGold = Math.Max(Settlement.CurrentSettlement.SettlementComponent.Gold, 0);
            int workersInParty = this.GetWorkersInParty();

            for (int i = 0; i < workersInParty; i += 1)
            {
                int wageTier = MBRandom.ChooseWeighted(new List<(int, float)> { (0, Tier0WageWeight), (1, Tier1WageWeight), (2, Tier2WageWeight), (3, Tier3WageWeight) });
                while (GetWagesFromTier(wageTier) > villageGold - goldEarned)
                {
                    wageTier -= 1;
                }

                switch (wageTier)
                {
                    case 0:
                        tier0WagesPaid += 1;
                        break;
                    case 1:
                        tier1WagesPaid += 1;
                        break;
                    case 2:
                        tier2WagesPaid += 1;
                        break;
                    case 3:
                        tier3WagesPaid += 1;
                        break;
                }

                goldEarned += GetWagesFromTier(wageTier);
            }

            if (tier0WagesPaid > 0)
            {
                MBTextManager.SetTextVariable("ps_tier0_wages_paid_count", tier0WagesPaid);
                InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=ps_tier0_wages_paid}{ps_tier0_wages_paid_count} of your party members were not paid for their labor.").ToString()));
            }

            if (tier1WagesPaid > 0)
            {
                MBTextManager.SetTextVariable("ps_tier1_wages_paid_count", tier1WagesPaid);
                MBTextManager.SetTextVariable("ps_tier1_wage", Tier1Wage);
                InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=ps_tier1_wages_paid}{ps_tier1_wages_paid_count} of your party members were paid {ps_tier1_wage}{GOLD_ICON} for their labor.").ToString()));
            }

            if (tier2WagesPaid > 0)
            {
                MBTextManager.SetTextVariable("ps_tier2_wages_paid_count", tier2WagesPaid);
                MBTextManager.SetTextVariable("ps_tier2_wage", Tier2Wage);
                InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=ps_tier2_wages_paid}{ps_tier2_wages_paid_count} of your party members were paid {ps_tier2_wage}{GOLD_ICON} for their labor.").ToString()));
            }

            if (tier3WagesPaid > 0)
            {
                MBTextManager.SetTextVariable("ps_tier3_wages_paid_count", tier3WagesPaid);
                MBTextManager.SetTextVariable("ps_tier3_wage", Tier3Wage);
                InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=ps_tier3_wages_paid}{ps_tier3_wages_paid_count} of your party members were paid {ps_tier3_wage}{GOLD_ICON} for their labor.").ToString()));
            }

            MBTextManager.SetTextVariable("ps_gold_earned", goldEarned);
            InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=ps_total_wages_paid}Your party was paid a total wage of {ps_gold_earned}{GOLD_ICON} for their labor.").ToString(), goldEarned > 0 ? "event:/ui/notification/coins_positive" : null));

            if (goldEarned > 0)
            {
                GiveGoldAction.ApplyForSettlementToCharacter(Settlement.CurrentSettlement, Hero.MainHero, goldEarned, true);
            }
        }

        internal void GrantWorkXp(Hero hero)
        {
            hero.AddSkillXp(DefaultSkills.Athletics, AthleticsXpPerHour);
        }

        internal int GetWorkersInParty()
        {
            int workersInParty = 0;
            foreach (TroopRosterElement member in PartyBase.MainParty.MemberRoster.GetTroopRoster())
            {
                if (member.Character.IsHero)
                {
                    if (member.Character.HeroObject == Hero.MainHero || !member.Character.HeroObject.IsWounded)
                    {
                        workersInParty += 1;
                    }
                }
                else if (member.Character.Tier == 0 && member.Character.Occupation == Occupation.Villager)
                {
                    workersInParty += member.Number - member.WoundedNumber;
                }
            }

            return workersInParty;
        }

        internal int GetWagesFromTier(int tier)
        {
            switch (tier)
            {
                case 0:
                    return Tier0Wage;

                case 1:
                    return Tier1Wage;

                case 2:
                    return Tier2Wage;

                case 3:
                    return Tier3Wage;

                default:
                    return 0;
            }
        }

        private void AddMenuOption(CampaignGameStarter campaignGameStarter)
        {
            // Village work option
            campaignGameStarter.AddGameMenuOption(
                "village",
                "ps_work",
                "{=ps_work}Work",
                (MenuCallbackArgs args) =>
                {
                    int workersInParty = this.GetWorkersInParty();
                    if (workersInParty > 1)
                    {
                        MBTextManager.SetTextVariable("ps_extra_workers", workersInParty - 1);
                        args.Tooltip = new TextObject("{=ps_work_with_party_tooltip}You and {ps_extra_workers} of your party members are willing and able to work.");
                    }
                    else
                    {
                        args.Tooltip = new TextObject("{=ps_work_without_party_tooltip}You alone are willing and able to work.");
                    }

                    args.optionLeaveType = GameMenuOption.LeaveType.Ransom;
                    return true;
                },
                (MenuCallbackArgs args) =>
                {
                    this.StartWorking();
                });

            // Working menu
            campaignGameStarter.AddWaitGameMenu(
                "ps_village_working",
                "{" + WorkingDescriptionId + "}",
                null,
                (MenuCallbackArgs args) =>
                {
                    this.UpdateWorkingMenu(args);
                    args.MenuContext.GameMenu.StartWait();
                    return true;
                },
                null,
                (MenuCallbackArgs args, CampaignTime dt) =>
                {
                    this.UpdateWorkingMenu(args);
                },
                GameMenu.MenuAndOptionType.WaitMenuShowOnlyProgressOption);

            // Stop working option
            campaignGameStarter.AddGameMenuOption(
                "ps_village_working",
                "ps_village_stop_working",
                "{=ps_leave]}Leave",
                (MenuCallbackArgs args) =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                (MenuCallbackArgs args) =>
                {
                    this.StopWorking();
                });
        }

        private void UpdateWorkingMenu(MenuCallbackArgs args)
        {
            int workersInParty = this.GetWorkersInParty();

            if (CampaignTime.Now.CurrentHourInDay < Utilities.PeasantWakeUpHour)
            {
                MBTextManager.SetTextVariable(WorkingDescriptionId, "{=ps_working_description_sleep}The laborers have gone to sleep.");
                args.MenuContext.SetBackgroundMeshName(Settlement.CurrentSettlement.SettlementComponent.WaitMeshName);
            }
            else if (CampaignTime.Now.CurrentHourInDay < Utilities.PeasantWorkHour)
            {
                MBTextManager.SetTextVariable(WorkingDescriptionId, "{=ps_working_description_too_early}The laborers are getting ready for their day.");
                args.MenuContext.SetBackgroundMeshName(Settlement.CurrentSettlement.SettlementComponent.WaitMeshName);
            }
            else if (CampaignTime.Now.CurrentHourInDay >= Utilities.PeasantSleepHour)
            {
                MBTextManager.SetTextVariable(WorkingDescriptionId, "{=ps_working_description_sleep}The laborers have gone to sleep.");
                args.MenuContext.SetBackgroundMeshName(Settlement.CurrentSettlement.SettlementComponent.WaitMeshName);
            }
            else if (CampaignTime.Now.CurrentHourInDay >= Utilities.PeasantLeisureHour)
            {
                MBTextManager.SetTextVariable(WorkingDescriptionId, "{=ps_working_description_too_late}The laborers have turned in for the day, and are relaxing around the village.");
                args.MenuContext.SetBackgroundMeshName(Settlement.CurrentSettlement.Culture.StringId + "_tavern");
            }
            else if (this.hoursWorked >= HoursInShift)
            {
                if (workersInParty > 1)
                {
                    MBTextManager.SetTextVariable(WorkingDescriptionId, "{=ps_working_description_finished_with_party}You and your party have finished their work for the day.");
                }
                else
                {
                    MBTextManager.SetTextVariable(WorkingDescriptionId, "{=ps_working_description_finished_without_party}You have finished your work for the day.");
                }

                args.MenuContext.SetBackgroundMeshName(Settlement.CurrentSettlement.Culture.StringId + "_tavern");
            }
            else
            {
                if (workersInParty > 1)
                {
                    MBTextManager.SetTextVariable("ps_extra_workers", workersInParty - 1);
                    MBTextManager.SetTextVariable(WorkingDescriptionId, "{=ps_working_description_work_with_party}You and {ps_extra_workers} of your party members work around the village.");
                }
                else
                {
                    MBTextManager.SetTextVariable("ps_working_description", "{=ps_working_description_work_without_party}You work around the village.");
                }

                args.MenuContext.SetBackgroundMeshName("encounter_peasant");
            }

            args.MenuContext.GameMenu.SetProgressOfWaitingInMenu(this.hoursWorked < HoursInShift ? (float)this.hoursWorked / HoursInShift : 0);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            this.AddMenuOption(starter);
        }

        private void OnHourlyTick()
        {
            if ((CampaignTime.Now.CurrentHourInDay >= Utilities.PeasantSleepHour || CampaignTime.Now.CurrentHourInDay < Utilities.PeasantWakeUpHour) && this.hoursWorked >= HoursInShift)
            {
                this.hoursWorked = 0;
            }

            if (isWorking)
            {
                this.DoWorkHour();
            }
        }

        private void OnVillageBeingRaided(Village village)
        {
            if (isWorking && village == Settlement.CurrentSettlement.Village)
            {
                this.StopWorking();
            }
        }
    }
}