using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("HiddenMines", "bmgjet", "1.0.0")]
    class HiddenMines : RustPlugin
    {
        private string permPlace = "HiddenMines.place"; // players can place a hidden mine
        private string permScan = "HiddenMines.scan"; // players can place a hidden mine
        private void Init() { permission.RegisterPermission(permPlace, this); permission.RegisterPermission(permScan, this); }
        private void OnEntitySpawned(Landmine land)
        {
            if (!permission.UserHasPermission(land.OwnerID.ToString(), permPlace)) { return; } //Check user has permission
            NextFrame(() => //Wait 1 frame for landmine to spawn
            {
                land.InvokeRepeating(() => { Hide(land); }, 0.5f, 0.1f); //Invoke loop
                //Create a stash
                StashContainer stash = GameManager.server.CreateEntity("assets/prefabs/deployable/small stash/small_stash_deployed.prefab", land.transform.position - (land.transform.up * 0.15f), land.transform.rotation) as StashContainer;
                stash.Spawn(); //Spawn it
                NextFrame(() => //Wait 1 frame for it to spawn
                {
                    stash.SetHidden(true); //Set it as hidden to trigger effect
                    stash.Invoke(() => //Delay 1 sec
                    {
                        stash.transform.position = Vector3.zero; //Move stash to vector.zero to stop its kill triggering mine
                        stash.Kill(); //Kill the stash
                    }, 1);
                });
            });
        }

        private void OnActiveItemChange(BasePlayer player, Item activeItem, uint itemID)
        {
            NextFrame(() => //Wait a frame for item to change
            {
                if (player?.GetActiveItem()?.info?.itemid == 999690781 && permission.UserHasPermission(player.UserIDString, permScan)) //Check for GeigerCounter and that player has permission
                {
                    MineDetector GC = player.GetComponent<MineDetector>(); //Check if player has component
                    if (GC == null) { GC = player.gameObject.AddComponent<MineDetector>(); } //Add component if player doesnt have it
                    GC.player = player; //Set player variable in component
                }
            });
        }

        private void Hide(BaseEntity ent)
        {
            if (ent.transform.position.y >= TerrainMeta.HeightMap.GetHeight(ent.transform.position) - 0.1f) //Check height
            {
                ent.transform.position -= new UnityEngine.Vector3(0, 0.01f, 0); //Sink mine into ground
                ent.SendNetworkUpdateImmediate(); //Update clinets
                return;
            }
            ent.CancelInvoke("Hide"); //Reached depth, stop invoke loop
        }

        public class MineDetector : MonoBehaviour
        {
            public BasePlayer player;
            private float Delay = 0; //Variable used to slow down method in fixedupdate
            public void FixedUpdate()
            {
                try 
                {
                    if (Delay >= UnityEngine.Time.realtimeSinceStartup) { return; } //Delay
                    if (player == null || player.IsSleeping() || player.GetActiveItem() == null || player?.GetActiveItem().info.itemid != 999690781) { Destroy(this); }  //Destroy component when conditions are no longer meet
                    Landmine lm = CloseLandMines(player.transform.position); //Scan for landmines
                    if (lm == null){Delay = UnityEngine.Time.realtimeSinceStartup + 1f;} //No landmines delay next scan
                    else//Found landmine increase scan and beep closer you get
                    {
                        Effect.server.Run("assets/prefabs/npc/autoturret/effects/targetacquired.prefab", player.transform.position); //Play turret sound as beep
                        Delay = UnityEngine.Time.realtimeSinceStartup + Mathf.Max(Vector3.Distance(player.transform.position, lm.transform.position) / 4, 0.1f); //Set beep speed
                    }
                }
                catch { Destroy(this); } //Something went wrong destoy this
            }
            private Landmine CloseLandMines(Vector3 pos)
            {

                Landmine mine = null;
                List<Landmine> list = new List<Landmine>();
                Vis.Entities<Landmine>(pos, 5, list, -1, QueryTriggerInteraction.Collide); //Scan area
                if (list.Count > 0) { //Found something
                    foreach (Landmine m in list)
                    {//Find the closes one
                        if (mine == null) { mine = m; }
                        else{if(Vector3.Distance(mine.transform.position,player.transform.position) > Vector3.Distance(m.transform.position, player.transform.position)){mine = m;}}
                    }
                }
                return mine;
            }
        }
    }
}