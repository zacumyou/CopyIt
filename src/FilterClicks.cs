// KO: 첫 클릭 이전 마스크를 저장해야 두 번째 클릭에서 단독/제외 전환을 정확히 판단할 수 있습니다.
// EN: Preserve the mask before the first click to distinguish solo and inverse on the second click.
namespace CopyIt
{
    internal sealed class FilterClicks
    {
        private int previous, beforeFirstClick;
        private double previousTime;
        internal int Apply(int mask,int type,double now,int all,out bool doubleClick){
            int before=beforeFirstClick;
            doubleClick=IsDoubleClick(type,now);
            if(doubleClick)return before==type?all&~type:type;
            beforeFirstClick=mask;return mask^type;
        }
        internal void Reset(){previous=0;previousTime=0;}
        internal bool IsDoubleClick(int type,double now)
        {
            if(previous==type&&now>=previousTime&&now-previousTime<=0.25){Reset();return true;}
            previous=type;previousTime=now;return false;
        }
    }
}
