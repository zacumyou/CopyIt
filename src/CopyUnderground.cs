// KO: 해발 Y가 아니라 게임의 터널 구성과 Elevation으로 지하 상태를 구분합니다.
// EN: Classifies underground state using native tunnel composition and Elevation, not world altitude.
using Game.Prefabs;
using Unity.Entities;
namespace CopyIt {
    public partial class CopyTool {
        internal bool IncludeUnderground {get;private set;}
        private bool IsUndergroundNetwork(Entity e){
            if(!IsNetwork(e))return false;
            bool tunnel=false;
            if(EntityManager.HasComponent<Game.Net.Upgraded>(e))tunnel=(EntityManager.GetComponentData<Game.Net.Upgraded>(e).m_Flags.m_General&CompositionFlags.General.Tunnel)!=0;
            if(EntityManager.HasComponent<Game.Net.Composition>(e)){
                var composition=EntityManager.GetComponentData<Game.Net.Composition>(e).m_Edge;
                if(EntityManager.HasComponent<NetCompositionData>(composition))tunnel|=(EntityManager.GetComponentData<NetCompositionData>(composition).m_Flags.m_General&CompositionFlags.General.Tunnel)!=0;
            }
            var elevation=EntityManager.HasComponent<Game.Net.Elevation>(e)?EntityManager.GetComponentData<Game.Net.Elevation>(e).m_Elevation:default;
            return SelectionMath.Underground(tunnel,elevation.x,elevation.y);
        }
        private void SetUndergroundSelection(bool include){
            if(Copying)return;
            IncludeUnderground=include;ResetRegion();pressed=false;hover=Entity.Null;
            if(!include)selected.RemoveWhere(IsUndergroundNetwork);
            SyncHighlights();Status=include?"지하 도로·철로 포함":"지하 도로·철로 제외";
            Diagnostics.Event("selection.underground",$"include={include} selected={selected.Count}");
        }
    }
}
