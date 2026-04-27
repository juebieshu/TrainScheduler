using System;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

// Token: 0x0200051F RID: 1311
public class TrainTrackBridgeAI : TrainTrackBaseAI
{
	// Token: 0x06003D2E RID: 15662 RVA: 0x00291860 File Offset: 0x0028FC60
	public override bool ColorizeProps(InfoManager.InfoMode infoMode)
	{
		return infoMode == InfoManager.InfoMode.Pollution || base.ColorizeProps(infoMode);
	}

	// Token: 0x06003D2F RID: 15663 RVA: 0x00291878 File Offset: 0x0028FC78
	public override int GetConstructionCost(Vector3 startPos, Vector3 endPos, float startHeight, float endHeight)
	{
		float num = VectorUtils.LengthXZ(endPos - startPos);
		float num2 = (Mathf.Max(0f, startHeight) + Mathf.Max(0f, endHeight)) * 0.5f;
		int num3 = Mathf.RoundToInt(num / 8f + 0.1f);
		int num4 = Mathf.RoundToInt(num2 / 4f + 0.1f);
		int num5 = this.m_constructionCost * num3 + this.m_elevationCost * num4;
		Singleton<EconomyManager>.instance.m_EconomyWrapper.OnGetConstructionCost(ref num5, this.m_info.m_class.m_service, this.m_info.m_class.m_subService, this.m_info.m_class.m_level);
		return num5;
	}

	// Token: 0x06003D30 RID: 15664 RVA: 0x00291930 File Offset: 0x0028FD30
	public override void GetNodeBuilding(ushort nodeID, ref NetNode data, out BuildingInfo building, out float heightOffset)
	{
		if ((data.m_flags & NetNode.Flags.Outside) == NetNode.Flags.None)
		{
			if (this.m_middlePillarInfo != null && (data.m_flags & NetNode.Flags.Double) != NetNode.Flags.None)
			{
				building = this.m_middlePillarInfo;
				heightOffset = this.m_middlePillarOffset - 1f - this.m_middlePillarInfo.m_generatedInfo.m_size.y;
				return;
			}
			if (this.m_bridgePillarInfo != null)
			{
				building = this.m_bridgePillarInfo;
				heightOffset = this.m_bridgePillarOffset - 1f - this.m_bridgePillarInfo.m_generatedInfo.m_size.y;
				return;
			}
		}
		base.GetNodeBuilding(nodeID, ref data, out building, out heightOffset);
	}

	// Token: 0x06003D31 RID: 15665 RVA: 0x002919DC File Offset: 0x0028FDDC
	public override float GetNodeInfoPriority(ushort segmentID, ref NetSegment data)
	{
		if ((data.m_flags & NetSegment.Flags.Untouchable) != NetSegment.Flags.None)
		{
			return this.m_info.m_halfWidth + 3000f;
		}
		if ((Singleton<NetManager>.instance.m_nodes.m_buffer[(int)data.m_startNode].m_flags & NetNode.Flags.Outside) != NetNode.Flags.None)
		{
			return this.m_info.m_halfWidth + 2000f;
		}
		if ((Singleton<NetManager>.instance.m_nodes.m_buffer[(int)data.m_endNode].m_flags & NetNode.Flags.Outside) != NetNode.Flags.None)
		{
			return this.m_info.m_halfWidth + 2000f;
		}
		if (this.m_bridgePillarInfo == null || !this.m_canModify)
		{
			return this.m_info.m_halfWidth - 1000f;
		}
		return this.m_info.m_halfWidth;
	}

	// Token: 0x06003D32 RID: 15666 RVA: 0x00291AB4 File Offset: 0x0028FEB4
	public override void ParentCollapsed(ushort segmentID, ref NetSegment data, InstanceManager.Group group)
	{
		if ((data.m_flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) != NetSegment.Flags.Created)
		{
			return;
		}
		if (!this.m_info.m_useFixedHeight)
		{
			return;
		}
		Singleton<NetManager>.instance.ReleaseSegment(segmentID, true);
	}

	// Token: 0x06003D33 RID: 15667 RVA: 0x00291AE2 File Offset: 0x0028FEE2
	public override bool RequireDoubleSegments()
	{
		return this.m_doubleLength;
	}

	// Token: 0x06003D34 RID: 15668 RVA: 0x00291AEA File Offset: 0x0028FEEA
	public override bool CanModify()
	{
		return this.m_canModify;
	}

	// Token: 0x06003D35 RID: 15669 RVA: 0x00291AF2 File Offset: 0x0028FEF2
	public override void GetElevationLimits(out int min, out int max)
	{
		min = 0;
		max = 5;
	}

	// Token: 0x06003D36 RID: 15670 RVA: 0x00291AFA File Offset: 0x0028FEFA
	public override bool IsOverground()
	{
		return true;
	}

	// Token: 0x06003D37 RID: 15671 RVA: 0x00291AFD File Offset: 0x0028FEFD
	public override bool CanIntersect(NetInfo with)
	{
		return false;
	}

	// Token: 0x0400439C RID: 17308
	public BuildingInfo m_bridgePillarInfo;

	// Token: 0x0400439D RID: 17309
	public BuildingInfo m_middlePillarInfo;

	// Token: 0x0400439E RID: 17310
	[CustomizableProperty("Elevation Cost", "Properties")]
	public int m_elevationCost = 2000;

	// Token: 0x0400439F RID: 17311
	[CustomizableProperty("Bridge Pillar Offset", "Properties")]
	public float m_bridgePillarOffset;

	// Token: 0x040043A0 RID: 17312
	[CustomizableProperty("Middle Pillar Offset", "Properties")]
	public float m_middlePillarOffset;

	// Token: 0x040043A1 RID: 17313
	[CustomizableProperty("Double Length", "Properties")]
	public bool m_doubleLength;

	// Token: 0x040043A2 RID: 17314
	[CustomizableProperty("Can Modify", "Properties")]
	public bool m_canModify = true;
}
