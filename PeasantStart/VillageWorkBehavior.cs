using System;
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
        private const int BaseWage = 4;
        private const float PercentWageIncreasePerWorker = 0.8f;
        private const float MinWageVariance = -0.3f;
        private const float MaxWageVariance = 0.2f;

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

            int workersInParty = this.GetWorkersInParty();
            float goldEarnedFraction = BaseWage;
            goldEarnedFraction *= 1 + (PercentWageIncreasePerWorker * (workersInParty - 1));
            goldEarnedFraction *= 1 + MBRandom.RandomFloatRanged(MinWageVariance, MaxWageVariance);
            int goldEarned = Math.Min((int)Math.Round(goldEarnedFraction), Settlement.CurrentSettlement.SettlementComponent.Gold);

            MBTextManager.SetTextVariable("ps_gold_earned", goldEarned);

            if (goldEarned > 0)
            {
                if (workersInParty > 1)
                {
                    InformationManager.DisplayMessage(
                        new InformationMessage(new TextObject("{=ps_wages_paid_with_party}Your party was paid a total wage of {ps_gold_earned}{GOLD_ICON} for their labor.").ToString(),
                        goldEarned > 0 ? "event:/ui/notification/coins_positive" : null));
                }
                else
                {
                    InformationManager.DisplayMessage(
                        new InformationMessage(new TextObject("{=ps_wages_paid_without_party}Your were paid a wage of {ps_gold_earned}{GOLD_ICON} for your labor.").ToString(),
                        goldEarned > 0 ? "event:/ui/notification/coins_positive" : null));
                }

                GiveGoldAction.ApplyForSettlementToCharacter(Settlement.CurrentSettlement, Hero.MainHero, goldEarned, true);
            }
            else
            {
                if (workersInParty > 1)
                {
                    InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=ps_wages_not_paid_with_party}The village was unable to pay your party for their labor.").ToString()));
                }
                else
                {
                    InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=ps_wages_not_paid_without_party}The village was unable to pay you for your labor.").ToString()));
                }
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
                MBTextManager.SetTextVariable(WorkingDescriptionId, "{=ps_working_description_sleep}The villagers have gone to sleep.");
                args.MenuContext.SetBackgroundMeshName(Settlement.CurrentSettlement.SettlementComponent.WaitMeshName);
            }
            else if (CampaignTime.Now.CurrentHourInDay < Utilities.PeasantWorkHour)
            {
                MBTextManager.SetTextVariable(WorkingDescriptionId, "{=ps_working_description_too_early}The villagers are getting ready for their day.");
                args.MenuContext.SetBackgroundMeshName(Settlement.CurrentSettlement.SettlementComponent.WaitMeshName);
            }
            else if (CampaignTime.Now.CurrentHourInDay >= Utilities.PeasantSleepHour)
            {
                MBTextManager.SetTextVariable(WorkingDescriptionId, "{=ps_working_description_sleep}The villagers have gone to sleep.");
                args.MenuContext.SetBackgroundMeshName(Settlement.CurrentSettlement.SettlementComponent.WaitMeshName);
            }
            else if (CampaignTime.Now.CurrentHourInDay >= Utilities.PeasantLeisureHour)
            {
                MBTextManager.SetTextVariable(WorkingDescriptionId, "{=ps_working_description_too_late}The villagers have turned in for the day, and are relaxing around the village.");
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