using System;
using System.Collections.Generic;
using Memoria.Data;
namespace FF9 { }
namespace Memoria
{
    public static class Configuration {public static class TetraMaster {public static int TripleTriad;}public static class Control {public static bool WrapSomeMenus;}}
}
namespace Memoria.Prime { public static class MathEx {public static int BitCount(byte n){int total=0;while(n!=0){total+=n&1;n>>=1;}return total;}} }
namespace Memoria.Data
{
    public sealed class TripleTriadCard {public byte atk,matk,pdef,mdef;}
    public static class TripleTriad {public static readonly Dictionary<TetraMasterCardId,TripleTriadCard> TripleTriadCardStats=new Dictionary<TetraMasterCardId,TripleTriadCard>();}
}
namespace UnityEngine {public static class Random {public static int Range(int minimum,int maximum){throw new InvalidOperationException("Presentation must not simulate card growth");}}}
public static class Debug {public static void Log(object value){}}
public static class CardPool {public const int TOTAL_CARDS=100;public static QuadMistCard GetMaxStatCard(TetraMasterCardId id){throw new InvalidOperationException("Presentation must not query growth limits");}}
public static class CardArrow
{
    public enum Type {UP,RIGHT_UP,RIGHT,RIGHT_DOWN,DOWN,LEFT_DOWN,LEFT,LEFT_UP}
    public static bool HasDirection(byte arrow,Type direction){return (arrow & (1<<(int)direction))!=0;}
}
public sealed partial class Board
{
    public static int SIZE_X=4,SIZE_Y=4;
    private readonly QuadMistCard[,] _cells=new QuadMistCard[4,4];
    public QuadMistCard this[int x,int y] {get{return _cells[x,y];}set{_cells[x,y]=value;}}
}
namespace Assets.Sources.Scripts.UI.Common {public static class FF9TextTool {public static string CardName(TetraMasterCardId id){return "Goblin";}}}
