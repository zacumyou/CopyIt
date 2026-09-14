// KO: 화면 다각형, 회전 및 경계 판정의 순수 수학 함수입니다.
// EN: Pure geometry helpers for screen polygons, rotation and boundary predicates.
using System;
using System.Collections.Generic;
namespace CopyIt
{
    public struct SelectionPlane
    {
        public float X,Y,Z,W;
        public SelectionPlane(float x,float y,float z,float w){X=x;Y=y;Z=z;W=w;}
        public bool Overlaps(float cx,float cy,float cz,float ex,float ey,float ez)=>
            X*cx+Y*cy+Z*cz+W+Math.Abs(X)*ex+Math.Abs(Y)*ey+Math.Abs(Z)*ez>=-0.01f;
    }
    public struct Point2 { public float X,Y; public Point2(float x,float y){X=x;Y=y;} }
    public struct Rect2
    {
        public float Left,Bottom,Right,Top;
        public Rect2(float x,float y,float x2,float y2){Left=Math.Min(x,x2);Right=Math.Max(x,x2);Bottom=Math.Min(y,y2);Top=Math.Max(y,y2);}
        public bool Contains(Point2 p)=>p.X>=Left&&p.X<=Right&&p.Y>=Bottom&&p.Y<=Top;
        public bool Intersects(Rect2 r)=>Left<=r.Right&&Right>=r.Left&&Bottom<=r.Top&&Top>=r.Bottom;
    }
    public static class SelectionMath
    {
        public static bool Underground(bool tunnel,float start,float end)=>tunnel||start<-.01f||end<-.01f;
        public static float WheelAngle(float angle,int notches)=>NormalizeAngle(angle+notches*.5f);
        public static float NormalizeAngle(float value) {value%=360;return value<0?value+360:value;}
        public static Point2 Rotate(float x,float z,float degrees)
        {double a=degrees*Math.PI/180;return new Point2((float)(x*Math.Cos(a)+z*Math.Sin(a)),(float)(z*Math.Cos(a)-x*Math.Sin(a)));}
        private static double Cross(Point2 a,Point2 b,Point2 c)=>(double)(b.X-a.X)*(c.Y-a.Y)-(double)(b.Y-a.Y)*(c.X-a.X);
        private static bool OnSegment(Point2 a,Point2 b,Point2 p)=>Math.Abs(Cross(a,b,p))<0.001&&new Rect2(a.X,a.Y,b.X,b.Y).Contains(p);
        public static bool Segments(Point2 a,Point2 b,Point2 c,Point2 d)
        {
            double ab=Cross(a,b,c),ac=Cross(a,b,d),cd=Cross(c,d,a),ce=Cross(c,d,b);
            return ((ab>0&&ac<0||ab<0&&ac>0)&&(cd>0&&ce<0||cd<0&&ce>0))||OnSegment(a,b,c)||OnSegment(a,b,d)||OnSegment(c,d,a)||OnSegment(c,d,b);
        }
        public static bool Contains(IReadOnlyList<Point2> polygon,Point2 p)
        {
            bool inside=false;
            for(int i=0,j=polygon.Count-1;i<polygon.Count;j=i++)
            {
                var a=polygon[j];var b=polygon[i];if(OnSegment(a,b,p))return true;
                if((a.Y>p.Y)!=(b.Y>p.Y)&&p.X<(b.X-a.X)*(p.Y-a.Y)/(b.Y-a.Y)+a.X)inside=!inside;
            }
            return inside;
        }
        public static bool Intersects(IReadOnlyList<Point2> p,Rect2 r)
        {
            if(p.Count<3)return false;
            var a=new Point2(r.Left,r.Bottom);var b=new Point2(r.Right,r.Bottom);var c=new Point2(r.Right,r.Top);var d=new Point2(r.Left,r.Top);
            if(Contains(p,a)||Contains(p,b)||Contains(p,c)||Contains(p,d))return true;
            for(int i=0,j=p.Count-1;i<p.Count;j=i++)
                if(r.Contains(p[i])||Segments(p[j],p[i],a,b)||Segments(p[j],p[i],b,c)||Segments(p[j],p[i],c,d)||Segments(p[j],p[i],d,a))return true;
            return false;
        }
        public static bool SimplePolygon(IReadOnlyList<Point2> p)
        {
            if(p.Count<3)return false;
            double area=0;
            for(int i=0;i<p.Count;i++)
            {
                var a=p[i];var b=p[(i+1)%p.Count];area+=(double)a.X*b.Y-(double)b.X*a.Y;
                for(int j=i+1;j<p.Count;j++)
                {if(j==i+1||(i==0&&j==p.Count-1))continue;if(Segments(a,b,p[j],p[(j+1)%p.Count]))return false;}
            }
            return Math.Abs(area)>1;
        }
    }
}
