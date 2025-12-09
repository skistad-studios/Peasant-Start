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
        private const int HoursInShift = 8;
        private const int ShiftStartHour = 4;
        private const int ShiftEndHour = 18;

        private const int Tier0Wage = 0;
        private const int Tier1Wage = 1;
        private const int Tier2Wage = 2;
        private const int Tier3Wage = 3;

        private const float Tier0WageWeight = 0.05f;
        private const float Tier1WageWeight = 0.7f;
        private const float Tier2WageWeight = 0.2f;
        private const float Tier3WageWeight = 0.05f;

        private const float AthleticsXpPerHour = 4.0f;

        private bool isWorking;
        private int hoursWorked;
        private int workStamina;

        public override void RegisterEvents()
        {
            CampaignEvents.OnCharacterCreationIsOverEvent.AddNonSerializedListener(this, OnCharacterCreationIsOver);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, this.OnSessionLaunched);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, this.OnHourlyTick);
            CampaignEvents.VillageBeingRaided.AddNonSerializedListener(this, this.OnVillageBeingRaided);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("ps_isWorking", ref this.isWorking);
            dataStore.SyncData("ps_hoursWorked", ref this.hoursWorked);
            dataStore.SyncData("ps_workStamina", ref this.workStamina);
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
            this.hoursWorked = 0;

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

            if (this.hoursWorked >= HoursInShift)
            {
                this.PayWages();
            }

            GameMenu.SwitchToMenu("village");
        }

        internal void WorkHour()
        {
            if (Settlement.CurrentSettlement == null)
            {
                return;
            }

            if (this.workStamina <= 0)
            {
                return;
            }

            this.hoursWorked += 1;
            this.workStamina -= 1;

            foreach (TroopRosterElement member in PartyBase.MainParty.MemberRoster.GetTroopRoster())
            {
                if ((member.Character.IsHero && member.Character.HeroObject == Hero.MainHero) || (member.Character.IsHero && !member.Character.HeroObject.IsWounded))
                {
                    this.GrantWorkXp(member.Character.HeroObject);
                }
            }

            if (this.hoursWorked >= HoursInShift)
            {
                this.StopWorking();
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
                while (GetWagesFromTier(wageTier) > villageGold)
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
                MBTextManager.SetTextVariable("ps_tier0_wages_paid", tier0WagesPaid);
                InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=ps_tier0_wages_paid}{ps_tier0_wages_paid} of your party members were not paid for their labor.").ToString()));
            }

            if (tier1WagesPaid > 0)
            {
                MBTextManager.SetTextVariable("ps_tier1_wages_paid", tier1WagesPaid);
                MBTextManager.SetTextVariable("ps_tier1_wage", Tier1Wage);
                InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=ps_tier1_wages_paid}{ps_tier1_wages_paid} of your party members were paid {ps_tier1_wage}{GOLD_ICON} for their labor.").ToString()));
            }

            if (tier2WagesPaid > 0)
            {
                MBTextManager.SetTextVariable("ps_tier2_wages_paid", tier2WagesPaid);
                MBTextManager.SetTextVariable("ps_tier2_wage", Tier2Wage);
                InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=ps_tier2_wages_paid}{ps_tier2_wages_paid} of your party members were paid {ps_tier2_wage}{GOLD_ICON} for their labor.").ToString()));
            }

            if (tier3WagesPaid > 0)
            {
                MBTextManager.SetTextVariable("ps_tier3_wages_paid", tier3WagesPaid);
                MBTextManager.SetTextVariable("ps_tier3_wage", Tier3Wage);
                InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=ps_tier3_wages_paid}{ps_tier3_wages_paid} of your party members were paid {ps_tier3_wage}{GOLD_ICON} for their labor.").ToString()));
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
                    bool canWork = this.workStamina >= HoursInShift && CampaignTime.Now.CurrentHourInDay >= ShiftStartHour && CampaignTime.Now.CurrentHourInDay < ShiftEndHour;
                    int workersInParty = this.GetWorkersInParty();
                    
                    if (canWork)
                    {
                        MBTextManager.SetTextVariable("ps_extra_workers", workersInParty - 1);
                        if (workersInParty > 1)
                        {
                            args.Tooltip = new TextObject("{=ps_work_with_party_tooltip}You and {ps_extra_workers} of your party members are willing and able to work.");
                        }
                        else
                        {
                            args.Tooltip = new TextObject("{=ps_work_without_party_tooltip}You alone are willing and able to work.");
                        }
                    }
                    else
                    {
                        if (CampaignTime.Now.CurrentHourInDay < ShiftStartHour)
                        {
                            args.Tooltip = new TextObject("{=ps_work_too_early_tooltip}The laborers are still asleep.");
                        }
                        else if (CampaignTime.Now.CurrentHourInDay >= ShiftEndHour)
                        {
                            args.Tooltip = new TextObject("{=ps_work_too_late_tooltip}The laborers have turned in for the day.");
                        }
                        else
                        {
                            MBTextManager.SetTextVariable("ps_hours_remaining_until_can_work", HoursInShift - this.workStamina);
                            if (workersInParty > 1)
                            {
                                args.Tooltip = new TextObject("{=ps_work_too_tired_with_party_tooltip}You and your party are too exhausted to work. You can work again in {ps_hours_remaining_until_can_work} hours.");
                            }
                            else
                            {
                                args.Tooltip = new TextObject("{=ps_work_too_tired_without_party_tooltip}You are too exhausted to work. You can work again in {ps_hours_remaining_until_can_work} hours.");
                            }
                        }
                    }

                    args.IsEnabled = canWork;
                    args.optionLeaveType = GameMenuOption.LeaveType.Wait;

                    return true;
                },
                (MenuCallbackArgs args) =>
                {
                    this.StartWorking();
                });

            // Working menu
            campaignGameStarter.AddWaitGameMenu(
                "ps_village_working",
                "{ps_working_description}",
                null,
                (MenuCallbackArgs args) =>
                {
                    int workersInParty = this.GetWorkersInParty();
                    if (workersInParty > 1)
                    {
                        MBTextManager.SetTextVariable("ps_extra_workers", workersInParty - 1);
                        MBTextManager.SetTextVariable("ps_working_description", "{=ps_working_with_party_description}You and {ps_extra_workers} of your party members work around the village.");
                    }
                    else
                    {
                        MBTextManager.SetTextVariable("ps_working_description", "{=ps_working_without_party_description}You work around the village.");
                    }

                    args.MenuContext.GameMenu.SetProgressOfWaitingInMenu(0);
                    args.MenuContext.GameMenu.StartWait();
                    return true;
                },
                null,
                (MenuCallbackArgs args, CampaignTime dt) =>
                {
                    args.MenuContext.GameMenu.SetProgressOfWaitingInMenu((float)this.hoursWorked / HoursInShift);
                },
                GameMenu.MenuAndOptionType.WaitMenuShowOnlyProgressOption);

            // Stop working option
            campaignGameStarter.AddGameMenuOption(
                "ps_village_working",
                "ps_village_stop_working",
                "{=ps_stop_working]}Stop working",
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

        private void OnCharacterCreationIsOver()
        {
            this.workStamina = HoursInShift;
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            this.AddMenuOption(starter);
        }

        private void OnHourlyTick()
        {
            if (isWorking)
            {
                this.WorkHour();
            }
            else if (this.workStamina < HoursInShift)
            {
                this.workStamina += 1;
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