using System;
using Memoria.Accessibility.Navigation;
using UnityEngine;

public partial class EventEngine
{
    public Obj TreadQuad(PosObj po, Int32 mode)
    {
        Int32 tag = (mode & 4) == 0 ? 2 : 3;
        return this.SelectQuad(po.go.transform.position, tag, po);
    }

    // Same read-only region selection as TreadQuad, at a prospective approach.
    // Push (mode 2) selects the first region with a tag 2 handler.
    public Obj NavigationTouchQuadAt(Single x, Single z)
    {
        return this.SelectQuad(new Vector3(x, 0, z), 2, null);
    }

    // Talk/Confirm (mode 4) selects the first region with a tag 3 handler.
    public Obj NavigationTalkQuadAt(Single x, Single z)
    {
        return this.SelectQuad(new Vector3(x, 0, z), 3, null);
    }

    private Obj SelectQuad(Vector3 position, Int32 tag, PosObj actor)
    {
        Quad doorstep = null;
        for (ObjList list = this._context.activeObj; list != null; list = list.next)
        {
            Obj obj = list.obj;
            if (obj == null || obj.cid != 3 || this.GetIP(obj.sid, tag, obj.ebData) == this.nil)
                continue;
            Quad quad = (Quad)obj;
            if (this.IsInQuadHotFix(quad, position.x, position.z))
                return quad;
            if (doorstep == null && this.IsInnDoorstep(quad, position, tag, actor))
                doorstep = quad;
        }
        // Any stock region has priority over the small doorstep allowance.
        return doorstep;
    }

    private Boolean IsInnDoorstep(Quad quad, Vector3 position, Int32 tag, PosObj actor)
    {
        return tag == 2 && this.gMode == 1 && NativeLindblumInnDoorstep.Contains(position.x, position.z) &&
            (actor == null || this.GetUserControl() && actor.uid == this.GetControlUID() && Math.Abs(position.y - NativeLindblumInnDoorstep.StreetHeight) < 1f) &&
            NativeLindblumInnDoorstep.Available(FF9StateSystem.Common.FF9.fldMapNo, this.fieldmap, quad);
    }

    public Boolean NavigationContainsQuad(Quad quad, Single x, Single z)
    {
        return quad != null && this.IsInQuadHotFix(quad, x, z);
    }

    private Boolean IsInQuad(Quad quad, Single x, Single z)
    {
        Int32 num = quad.n;
        for (Int32 index1 = 0; index1 < num; ++index1)
        {
            Int32 index2 = index1 % num;
            Int32 index3 = (index1 + 1) % num;
            Int32 index4 = (index1 + 2) % num;
            Vector3 vector3Val1 = quad.q[index2].Vector3Val;
            Vector3 vector3Val2 = quad.q[index3].Vector3Val;
            Vector3 vector3Val3 = quad.q[index4].Vector3Val;
            if (Math3D.PointInsideTriangleTestXZ(new Vector3(x, 0.0f, z), vector3Val1, vector3Val2, vector3Val3))
                return true;
        }
        return false;
    }

    private Boolean IsInQuadHotFix(Quad quad, Single x, Single z)
    {
        if (this.gMode != 1)
            return this.IsInQuad(quad, x, z);
        Int32 npcid = EMinigame.CreateNPCID((Int32)FF9StateSystem.Common.FF9.fldMapNo, (Int32)quad.sid);
        if (!EventEngineUtils.QuadCircleData.ContainsKey(npcid))
            return this.IsInQuad(quad, x, z);
        QuadCircle quadCircle = EventEngineUtils.QuadCircleData[npcid];
        if (!quadCircle.UseOriginalArea)
            return quadCircle.IsCollisionEnter(x, z);
        if (!quadCircle.IsCollisionEnter(x, z))
            return this.IsInQuad(quad, x, z);
        return true;
    }
}
