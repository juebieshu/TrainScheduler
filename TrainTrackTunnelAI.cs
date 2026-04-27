using System;
using UnityEngine;

// Token: 0x02000520 RID: 1312
public class TrainTrackTunnelAI : TrainTrackBaseAI
{
	// Token: 0x06003D39 RID: 15673 RVA: 0x00291B0F File Offset: 0x0028FF0F
	public override bool ColorizeProps(InfoManager.InfoMode infoMode)
	{
		return infoMode == InfoManager.InfoMode.Pollution || base.ColorizeProps(infoMode);
	}

	// Token: 0x06003D3A RID: 15674 RVA: 0x00291B28 File Offset: 0x0028FF28
	public override Color GetColor(ushort segmentID, ref NetSegment data, InfoManager.InfoMode infoMode, InfoManager.SubInfoMode subInfoMode)
	{
		switch (infoMode)
		{
		case InfoManager.InfoMode.TrafficRoutes:
		case InfoManager.InfoMode.Underground:
		case InfoManager.InfoMode.Tours:
			break;
		default:
			if (infoMode != InfoManager.InfoMode.Transport)
			{
				if (infoMode == InfoManager.InfoMode.Traffic)
				{
					return base.GetColor(segmentID, ref data, infoMode, subInfoMode) * 0.2f;
				}
				if (infoMode != InfoManager.InfoMode.EscapeRoutes)
				{
					return base.GetColor(segmentID, ref data, infoMode, subInfoMode);
				}
			}
			break;
		}
		return new Color(0.2f, 0.2f, 0.2f, 1f);
	}

	// Token: 0x06003D3B RID: 15675 RVA: 0x00291BA4 File Offset: 0x0028FFA4
	public override Color GetColor(ushort nodeID, ref NetNode data, InfoManager.InfoMode infoMode, InfoManager.SubInfoMode subInfoMode)
	{
		switch (infoMode)
		{
		case InfoManager.InfoMode.TrafficRoutes:
		case InfoManager.InfoMode.Underground:
		case InfoManager.InfoMode.Tours:
			break;
		default:
			if (infoMode != InfoManager.InfoMode.Transport)
			{
				if (infoMode == InfoManager.InfoMode.Traffic)
				{
					return base.GetColor(nodeID, ref data, infoMode, subInfoMode) * 0.2f;
				}
				if (infoMode != InfoManager.InfoMode.EscapeRoutes)
				{
					return base.GetColor(nodeID, ref data, infoMode, subInfoMode);
				}
			}
			break;
		}
		return new Color(0.2f, 0.2f, 0.2f, 1f);
	}

	// Token: 0x06003D3C RID: 15676 RVA: 0x00291C1D File Offset: 0x0029001D
	public override float GetNodeInfoPriority(ushort segmentID, ref NetSegment data)
	{
		if (this.m_info.m_flattenTerrain)
		{
			return this.m_info.m_halfWidth + 9000f;
		}
		return this.m_info.m_halfWidth + 10000f;
	}

	// Token: 0x06003D3D RID: 15677 RVA: 0x00291C52 File Offset: 0x00290052
	public override bool IsUnderground()
	{
		return true;
	}

	// Token: 0x06003D3E RID: 15678 RVA: 0x00291C55 File Offset: 0x00290055
	public override bool BuildUnderground()
	{
		return !this.m_info.m_flattenTerrain;
	}

	// Token: 0x06003D3F RID: 15679 RVA: 0x00291C65 File Offset: 0x00290065
	public override bool CanModify()
	{
		return this.m_canModify;
	}

	// Token: 0x06003D40 RID: 15680 RVA: 0x00291C6D File Offset: 0x0029006D
	public override bool CollapseSegment(ushort segmentID, ref NetSegment data, InstanceManager.Group group, bool demolish)
	{
		return demolish && base.CollapseSegment(segmentID, ref data, group, demolish);
	}

	// Token: 0x06003D41 RID: 15681 RVA: 0x00291C83 File Offset: 0x00290083
	public override void ParentCollapsed(ushort segmentID, ref NetSegment data, InstanceManager.Group group)
	{
	}

	// Token: 0x06003D42 RID: 15682 RVA: 0x00291C85 File Offset: 0x00290085
	public override void GetTerrainModifyRange(out float start, out float end)
	{
		start = ((!this.m_info.m_flattenTerrain) ? 0f : 0.25f);
		end = 1f;
	}

	// Token: 0x06003D43 RID: 15683 RVA: 0x00291CAF File Offset: 0x002900AF
	public override void GetElevationLimits(out int min, out int max)
	{
		min = ((!this.m_info.m_flattenTerrain) ? (-3) : 0);
		max = 0;
	}

	// Token: 0x06003D44 RID: 15684 RVA: 0x00291CCE File Offset: 0x002900CE
	public override bool CanIntersect(NetInfo with)
	{
		return false;
	}

	// Token: 0x06003D45 RID: 15685 RVA: 0x00291CD1 File Offset: 0x002900D1
	public override bool RaiseTerrain()
	{
		return true;
	}

	// Token: 0x040043A3 RID: 17315
	[CustomizableProperty("Can Modify", "Properties")]
	public bool m_canModify = true;
}
