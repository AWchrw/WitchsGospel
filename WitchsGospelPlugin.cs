using BepInEx;
using BepInEx.Logging;
using R2API;
using RoR2;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace WitchsGospel
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class WitchsGospelPlugin : BaseUnityPlugin
    {
        public const string PluginGUID    = "com.yourname.witchsgospel";
        public const string PluginName    = "WitchsGospel";
        public const string PluginVersion = "2.0.0";

        internal static ManualLogSource Log;
        public static ItemDef WitchsGospelItemDef;

        // key = NetworkUser.id (stable across scene reloads)
        private static readonly Dictionary<NetworkUserId, SavedPlayerState> savedStates
            = new Dictionary<NetworkUserId, SavedPlayerState>();

        // players ที่กำลัง return (ป้องกัน double-trigger)
        private static readonly HashSet<NetworkUserId> returning
            = new HashSet<NetworkUserId>();

        // scene name ของ stage ปัจจุบัน (เพื่อเช็คว่า reload = return หรือ stage ใหม่)
        private static string lastSceneName = "";

        // ---------------------------------------------------------------
        public void Awake()
        {
            Log = Logger;
            RegisterItem();

            Stage.onStageStartGlobal += OnStageStart;
            On.RoR2.CharacterMaster.OnBodyDeath += OnBodyDeath;

            // set tier หลัง catalog พร้อม
            RoR2.ItemCatalog.availability.CallWhenAvailable(() =>
            {
                if (WitchsGospelItemDef != null)
                    WitchsGospelItemDef.tier = ItemTier.Tier3;
            });

            // ดัก inventory changed → update snapshot/charges ถ้าได้ item หลัง stage start
            Inventory.onInventoryChangedGlobal += OnInventoryChanged;

            Log.LogInfo($"{PluginName} v{PluginVersion} loaded!");
        }

        private void OnDestroy()
        {
            Stage.onStageStartGlobal -= OnStageStart;
            On.RoR2.CharacterMaster.OnBodyDeath -= OnBodyDeath;
            Inventory.onInventoryChangedGlobal -= OnInventoryChanged;
        }

        // ---------------------------------------------------------------
        //  ITEM REGISTRATION
        // ---------------------------------------------------------------
        private void RegisterItem()
        {
            WitchsGospelItemDef = ScriptableObject.CreateInstance<ItemDef>();
            WitchsGospelItemDef.name             = "WitchsGospel";
            WitchsGospelItemDef.nameToken        = "WITCHSGOSPEL_NAME";
            WitchsGospelItemDef.pickupToken      = "WITCHSGOSPEL_PICKUP";
            WitchsGospelItemDef.descriptionToken = "WITCHSGOSPEL_DESC";
            WitchsGospelItemDef.loreToken        = "WITCHSGOSPEL_LORE";
            WitchsGospelItemDef.tier             = ItemTier.Tier3;
            WitchsGospelItemDef.tags             = new ItemTag[] { ItemTag.Utility, ItemTag.CannotCopy };
            WitchsGospelItemDef.canRemove        = true;
            WitchsGospelItemDef.hidden           = false;
            WitchsGospelItemDef.pickupIconSprite = Resources.Load<Sprite>("Textures/ItemIcons/texMysteryIcon");

            ItemAPI.Add(new CustomItem(WitchsGospelItemDef, new ItemDisplayRuleDict(null)));

            LanguageAPI.Add("WITCHSGOSPEL_NAME",   "Witch's Gospel");
            LanguageAPI.Add("WITCHSGOSPEL_PICKUP",
                "Cheat death once per stage. The stage resets to how it was when you arrived.");
            LanguageAPI.Add("WITCHSGOSPEL_DESC",
                "<style=cIsUtility>Once per stage</style> per stack: upon death, " +
                "<style=cIsHealing>the stage fully restarts</style> and your " +
                "<style=cIsUtility>inventory resets to what it was at stage start</style>. " +
                "When all charges are spent, death is permanent.");
            LanguageAPI.Add("WITCHSGOSPEL_LORE",
                "\"I die. I come back.\n" +
                "And no matter how many times I return, the pain never fades.\"\n\n" +
                "The book smells faintly of white flowers.\n" +
                "Its pages are blank — yet somehow, you already know how the story ends.");

            Log.LogInfo("Witch's Gospel registered.");
        }

        // ---------------------------------------------------------------
        //  INVENTORY CHANGED — handle late give_item (e.g. stage 1 console)
        // ---------------------------------------------------------------
        private void OnInventoryChanged(Inventory inv)
        {
            if (!NetworkServer.active || WitchsGospelItemDef == null) return;

            var master = inv.GetComponent<CharacterMaster>();
            if (master == null) return;

            var userId = GetUserId(master);
            if (!userId.HasValue) return;

            int stacks = inv.GetItemCountPermanent(WitchsGospelItemDef.itemIndex);
            if (stacks == 0) return;

            // ถ้ายังไม่มี snapshot → สร้างทันที (เช่น give_item ตอน stage 1)
            if (!savedStates.ContainsKey(userId.Value))
            {
                var state = SavedPlayerState.From(master);
                state.totalGospelStacks = stacks;
                savedStates[userId.Value] = state;
                Log.LogDebug($"[InvChanged] Late snapshot for {userId.Value} stacks={stacks}");
            }
        }

        // ---------------------------------------------------------------
        //  STAGE START — snapshot inventory + restore stacks
        // ---------------------------------------------------------------
        private void OnStageStart(Stage stage)
        {
            if (!NetworkServer.active) return;

            string currentScene = stage.sceneDef?.baseSceneName ?? "";
            bool isReturnReload = currentScene == lastSceneName && lastSceneName != "";
            lastSceneName = ""; // reset ทันที ป้องกัน false positive

            Log.LogInfo($"[StageStart] scene={currentScene} isReturnReload={isReturnReload}");

            foreach (var pmc in PlayerCharacterMasterController.instances)
            {
                var master = pmc?.master;
                if (master == null) continue;

                var userId = GetUserId(master);
                if (!userId.HasValue) continue;

                var id = userId.Value;

                returning.Remove(id);

                if (isReturnReload)
                {
                    // Return reload → restore inventory แต่ไม่คืน gospel stack
                    if (savedStates.ContainsKey(id))
                        savedStates[id].RestoreInventory(master);

                    // snapshot ใหม่ (gospel stack ยังลดอยู่)
                    int stacks = GetItemCount(master);
                    var retState = SavedPlayerState.From(master);
                    retState.totalGospelStacks = savedStates.ContainsKey(id)
                        ? savedStates[id].totalGospelStacks  // เก็บ total เดิมไว้
                        : stacks;
                    savedStates[id] = retState;
                    Log.LogDebug($"[StageStart-Return] {id} activeStacks={stacks} total={retState.totalGospelStacks}");
                }
                else
                {
                    // Stage ใหม่จริงๆ → คืน stack ที่ถูก remove ทั้งหมด
                    if (savedStates.ContainsKey(id))
                    {
                        int current = GetItemCount(master);
                        int restore = savedStates[id].totalGospelStacks - current;
                        if (restore > 0)
                        {
                            master.inventory.GiveItemPermanent(WitchsGospelItemDef.itemIndex, restore);
                            Log.LogInfo($"[StageStart-New] Restored {restore} stack(s) for {id}");
                        }
                    }

                    // snapshot ใหม่
                    int stacks = GetItemCount(master);
                    var newState = SavedPlayerState.From(master);
                    newState.totalGospelStacks = stacks;
                    savedStates[id] = newState;
                    Log.LogDebug($"[StageStart-New] {id} stacks={stacks} items={newState.items.Count}");
                }
            }
        }

        // ---------------------------------------------------------------
        //  DEATH HOOK
        // ---------------------------------------------------------------
        private void OnBodyDeath(On.RoR2.CharacterMaster.orig_OnBodyDeath orig, CharacterMaster self, CharacterBody body)
        {
            // ไม่ใช่ player หรือกำลัง return อยู่แล้ว → ผ่านไป
            if (!NetworkServer.active || self.playerCharacterMasterController == null)
            {
                orig(self, body);
                return;
            }

            var userId = GetUserId(self);
            if (!userId.HasValue)
            {
                orig(self, body);
                return;
            }

            var id = userId.Value;

            // กำลัง return อยู่แล้ว → ป้องกัน double trigger
            if (returning.Contains(id))
            {
                orig(self, body);
                return;
            }

            int stacks = GetItemCount(self);
            if (stacks == 0)
            {
                Log.LogInfo($"[WitchsGospel] {id} has no stacks — permanent death.");
                orig(self, body);
                return;
            }

            // --- ใช้ charge: remove 1 stack ออก ---
            self.inventory.RemoveItemPermanent(WitchsGospelItemDef.itemIndex, 1);
            returning.Add(id);
            self.preventGameOver = true;

            Log.LogInfo($"[WitchsGospel] Return by Death for {id}! Stacks left: {stacks - 1}");

            var state = savedStates.ContainsKey(id)
                ? savedStates[id]
                : SavedPlayerState.From(self);

            orig(self, body);

            StartCoroutine(DoReturn(self, id, state, stacks));
        }

        // ---------------------------------------------------------------
        //  RETURN COROUTINE
        // ---------------------------------------------------------------
        private IEnumerator DoReturn(CharacterMaster master, NetworkUserId id, SavedPlayerState state, int stacksBefore)
        {
            yield return null;

            // restore inventory (ไม่แตะ gospel stack)
            state.RestoreInventory(master);

            int remaining = stacksBefore - 1;
            Chat.AddMessage(
                $"<color=#cc88ff><b>Witch's Gospel</b></color> — Return by Death! " +
                $"Charges this stage: <b>{remaining}</b>");

            yield return new WaitForSeconds(1.5f);

            // reload scene เดิม
            var sceneDef = SceneCatalog.GetSceneDefForCurrentScene();
            if (sceneDef != null && NetworkServer.active)
            {
                lastSceneName = sceneDef.baseSceneName; // จำ scene ที่กำลัง reload
                Log.LogInfo($"[WitchsGospel] Reloading: {lastSceneName}");
                NetworkManager.singleton.ServerChangeScene(lastSceneName);
            }
        }

        // ---------------------------------------------------------------
        //  HELPERS
        // ---------------------------------------------------------------
        private static NetworkUserId? GetUserId(CharacterMaster master)
        {
            var pmc = master?.playerCharacterMasterController;
            if (pmc == null) return null;
            var netUser = pmc.networkUser;
            if (netUser == null) return null;
            return netUser.id;
        }

        private static int GetItemCount(CharacterMaster master)
        {
            var inv = master?.inventory;
            if (inv == null || WitchsGospelItemDef == null) return 0;
            return inv.GetItemCountPermanent(WitchsGospelItemDef.itemIndex);
        }
    }

    // ---------------------------------------------------------------
    //  SAVE STATE
    // ---------------------------------------------------------------
    public class SavedPlayerState
    {
        public Dictionary<ItemIndex, int> items            = new Dictionary<ItemIndex, int>();
        public EquipmentIndex             equipment         = EquipmentIndex.None;
        public int                        totalGospelStacks = 0;

        public static SavedPlayerState From(CharacterMaster master)
        {
            var state = new SavedPlayerState();
            var inv   = master?.inventory;
            if (inv == null) return state;

            foreach (var idx in ItemCatalog.allItems)
            {
                if (WitchsGospelPlugin.WitchsGospelItemDef != null &&
                    idx == WitchsGospelPlugin.WitchsGospelItemDef.itemIndex) continue;
                int count = inv.GetItemCountPermanent(idx);
                if (count > 0) state.items[idx] = count;
            }
            state.equipment = inv.GetEquipmentIndex();
            return state;
        }

        public void RestoreInventory(CharacterMaster master)
        {
            var inv = master?.inventory;
            if (inv == null) return;

            foreach (var idx in ItemCatalog.allItems)
            {
                if (WitchsGospelPlugin.WitchsGospelItemDef != null &&
                    idx == WitchsGospelPlugin.WitchsGospelItemDef.itemIndex) continue;

                int current = inv.GetItemCountPermanent(idx);
                int saved   = items.ContainsKey(idx) ? items[idx] : 0;
                int delta   = current - saved;

                if (delta > 0)      inv.RemoveItemPermanent(idx, delta);
                else if (delta < 0) inv.GiveItemPermanent(idx, -delta);
            }

            if (equipment != EquipmentIndex.None)
                inv.SetEquipmentIndex(equipment, false);
        }
    }
}
