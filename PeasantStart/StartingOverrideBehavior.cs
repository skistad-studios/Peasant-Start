using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace PeasantStart
{
    internal class StartingOverrideBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnCharacterCreationIsOverEvent.AddNonSerializedListener(this, OnCharacterCreationIsOver);
        }

        public override void SyncData(IDataStore dataStore) { }

        internal void EquipPeasantSet()
        {
            for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i += 1)
            {
                Hero.MainHero.BattleEquipment[i] = EquipmentElement.Invalid;
                Hero.MainHero.CivilianEquipment[i] = EquipmentElement.Invalid;
                Hero.MainHero.StealthEquipment[i] = EquipmentElement.Invalid;
            }

            Hero.MainHero.BattleEquipment[EquipmentIndex.Body] = new EquipmentElement(MBObjectManager.Instance.GetObject<ItemObject>("ps_peasant_rags"));
            Hero.MainHero.CivilianEquipment[EquipmentIndex.Body] = new EquipmentElement(MBObjectManager.Instance.GetObject<ItemObject>("ps_peasant_rags"));
            Hero.MainHero.StealthEquipment[EquipmentIndex.Body] = new EquipmentElement(MBObjectManager.Instance.GetObject<ItemObject>("ps_peasant_rags"));
            Hero.MainHero.StealthEquipment[EquipmentIndex.Weapon0] = new EquipmentElement(MBObjectManager.Instance.GetObject<ItemObject>("stealth_throwing_stone"));
        }

        internal void ClearInventory()
        {
            PartyBase.MainParty.ItemRoster.Clear();
        }

        internal void ClearGold()
        {
            Hero.MainHero.Gold = 0;
        }

        private void OnCharacterCreationIsOver()
        {
            this.EquipPeasantSet();
            this.ClearInventory();
            this.ClearGold();
        }
    }
}