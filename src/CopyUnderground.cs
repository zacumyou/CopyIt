// KO: 해발 Y가 아니라 게임의 터널 구성과 Elevation으로 지하 상태를 구분합니다.
// EN: Classifies underground state using native tunnel composition and Elevation, not world altitude.
using Game.Prefabs;
using Unity.Entities;
namespace CopyIt {
    public partial class CopyTool {
        private bool undergroundView;
        public override bool allowUnderground => true;
        public override void ElevationDown()=>SetUnderground(true);
        public override void ElevationUp()=>SetUnderground(false);
        public override void ElevationScroll()=>SetUnderground(!undergroundView);
        public override void SetUnderground(bool underground){
            if(undergroundView==underground){requireUnderground=underground;return;}
            undergroundView=underground;requireUnderground=underground;
            // KO/EN: 지하 진입은 선택 필터도 켭니다 / Entering underground also enables its selection filter.
            if(underground)IncludeUnderground=true;
            if(!Copying&&!Deleting){selected.Clear();sampleCandidates.Clear();ResetRegion();pressed=false;hover=Entity.Null;SyncHighlights();}
            Diagnostics.Event("view.underground",$"enabled={underground} copying={Copying} include={IncludeUnderground}");
        }
        private bool MatchesView(Entity e){
            if(!undergroundView)return true;
            if(!Live(e))return false;
            if(IsNetwork(e))return IsUndergroundNetwork(e);
            if(!EntityManager.HasComponent<Game.Objects.Transform>(e))return false;
            var position=EntityManager.GetComponentData<Game.Objects.Transform>(e).m_Position;
            return position.y<GroundAt(position)-.1f;
        }
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
            if(!include&&undergroundView)SetUnderground(false);
            IncludeUnderground=include;ResetRegion();pressed=false;hover=Entity.Null;
            if(!include)selected.RemoveWhere(IsUndergroundNetwork);
            SyncHighlights();Status=include?L10n.Message("CopyIt.Text.F6A4B5B56A"):L10n.Message("CopyIt.Text.9600604679");
            Diagnostics.Event("selection.underground",$"include={include} selected={selected.Count}");
        }
    }
}
