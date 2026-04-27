using System;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

// Token: 0x0200051E RID: 1310
public class TrainTrackBaseAI : PlayerNetAI
{
	// Token: 0x06003D0F RID: 15631 RVA: 0x0028097C File Offset: 0x0027ED7C
	public override void RenderNode(ushort nodeID, ref NetNode nodeData, RenderManager.CameraInfo cameraInfo)
	{
		if ((nodeData.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None && Singleton<InfoManager>.instance.CurrentMode == InfoManager.InfoMode.TrafficRoutes && Singleton<InfoManager>.instance.CurrentSubMode == InfoManager.SubInfoMode.WaterPower && (this.m_info.m_vehicleTypes & VehicleInfo.VehicleType.Train) != VehicleInfo.VehicleType.None)
		{
			Vector3 position = nodeData.m_position;
			position.y += this.GetSnapElevation();
			float num = Vector3.Distance(cameraInfo.m_position, position);
			float num2 = MathUtils.SmoothStep(1000f, 500f, num);
			if (num2 < 0.001f)
			{
				return;
			}
			bool flag = (nodeData.m_flags & NetNode.Flags.OneWayIn) == NetNode.Flags.None;
			if (!flag)
			{
				return;
			}
			InstanceID instanceID;
			int num3;
			Singleton<ToolManager>.instance.m_properties.GetComponent<DefaultTool>().GetHoverInstance(out instanceID, out num3);
			if (flag)
			{
				NetManager instance = Singleton<NetManager>.instance;
				float num4 = this.GetCollisionHalfWidth() * 0.75f;
				for (int i = 0; i < 8; i++)
				{
					ushort segment = nodeData.GetSegment(i);
					if (segment != 0)
					{
						NetInfo info = instance.m_segments.m_buffer[(int)segment].Info;
						bool flag2 = instance.m_segments.m_buffer[(int)segment].m_startNode == nodeID;
						bool flag3 = (instance.m_segments.m_buffer[(int)segment].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;
						if ((flag2 != flag3) ? info.m_hasBackwardVehicleLanes : info.m_hasForwardVehicleLanes)
						{
							float num5 = ((instanceID.NetNode != nodeID || num3 != i + 1) ? 0.75f : 1f);
							Vector3 vector = ((!flag2) ? instance.m_segments.m_buffer[(int)segment].m_endDirection : instance.m_segments.m_buffer[(int)segment].m_startDirection);
							Vector3 vector2 = position + vector * num4;
							NetSegment.Flags flags = ((!flag2) ? NetSegment.Flags.YieldEnd : NetSegment.Flags.YieldStart);
							if ((instance.m_segments.m_buffer[(int)segment].m_flags & flags) != NetSegment.Flags.None)
							{
								NotificationEvent.RenderInstance(cameraInfo, NotificationEvent.Type.YieldOn, vector2, num5, num2);
							}
							else
							{
								NotificationEvent.RenderInstance(cameraInfo, NotificationEvent.Type.YieldOff, vector2, num5, num2);
							}
						}
					}
				}
			}
		}
	}

	// Token: 0x06003D10 RID: 15632 RVA: 0x00280BD0 File Offset: 0x0027EFD0
	public override Color GetColor(ushort segmentID, ref NetSegment data, InfoManager.InfoMode infoMode, InfoManager.SubInfoMode subInfoMode)
	{
		if (infoMode == InfoManager.InfoMode.NoisePollution)
		{
			int num = (int)(100 - (data.m_noiseDensity - 100) * (data.m_noiseDensity - 100) / 100);
			int num2 = this.m_noiseAccumulation * num / 100;
			return CommonBuildingAI.GetNoisePollutionColor((float)num2 * 1.25f);
		}
		if (infoMode != InfoManager.InfoMode.Transport)
		{
			if (infoMode == InfoManager.InfoMode.None)
			{
				Color color = this.m_info.m_color;
				if ((data.m_flags & NetSegment.Flags.Collapsed) != NetSegment.Flags.None)
				{
					float num3 = 0.5f;
					color.r = color.r * (1f - num3) + num3 * 0.75f;
					color.g = color.g * (1f - num3) + num3 * 0.75f;
					color.b = color.b * (1f - num3) + num3 * 0.75f;
				}
				color.a = (float)(byte.MaxValue - data.m_wetness) * 0.003921569f;
				return color;
			}
			if (infoMode == InfoManager.InfoMode.Traffic)
			{
				return Color.Lerp(Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_targetColor, Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_negativeColor, Mathf.Clamp01((float)data.m_trafficDensity * 0.01f));
			}
			if (infoMode != InfoManager.InfoMode.Destruction)
			{
				return base.GetColor(segmentID, ref data, infoMode, subInfoMode);
			}
			if ((data.m_flags & NetSegment.Flags.Collapsed) != NetSegment.Flags.None)
			{
				return Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_negativeColor;
			}
			return Singleton<InfoManager>.instance.m_properties.m_neutralColor;
		}
		else
		{
			if (this.m_info.m_class.m_subService == ItemClass.SubService.PublicTransportMonorail)
			{
				return Color.Lerp(Singleton<InfoManager>.instance.m_properties.m_neutralColor, Singleton<TransportManager>.instance.m_properties.m_transportColors[8], 0.2f);
			}
			return Color.Lerp(Singleton<InfoManager>.instance.m_properties.m_neutralColor, Singleton<TransportManager>.instance.m_properties.m_transportColors[2], 0.2f);
		}
	}

	// Token: 0x06003D11 RID: 15633 RVA: 0x00280DD1 File Offset: 0x0027F1D1
	public override bool ColorizeProps(InfoManager.InfoMode infoMode)
	{
		return infoMode == InfoManager.InfoMode.Traffic || base.ColorizeProps(infoMode);
	}

	// Token: 0x06003D12 RID: 15634 RVA: 0x00280DEC File Offset: 0x0027F1EC
	public override Color GetColor(ushort nodeID, ref NetNode data, InfoManager.InfoMode infoMode, InfoManager.SubInfoMode subInfoMode)
	{
		if (infoMode == InfoManager.InfoMode.NoisePollution)
		{
			int num = 0;
			if (this.m_noiseAccumulation != 0)
			{
				NetManager instance = Singleton<NetManager>.instance;
				int num2 = 0;
				for (int i = 0; i < 8; i++)
				{
					ushort segment = data.GetSegment(i);
					if (segment != 0)
					{
						num += (int)instance.m_segments.m_buffer[(int)segment].m_noiseDensity;
						num2++;
					}
				}
				if (num2 != 0)
				{
					num /= num2;
				}
			}
			int num3 = 100 - (num - 100) * (num - 100) / 100;
			int num4 = this.m_noiseAccumulation * num3 / 100;
			return CommonBuildingAI.GetNoisePollutionColor((float)num4 * 1.25f);
		}
		if (infoMode != InfoManager.InfoMode.Transport)
		{
			if (infoMode == InfoManager.InfoMode.None)
			{
				int num5 = 0;
				int num6 = 0;
				NetManager instance2 = Singleton<NetManager>.instance;
				int num7 = 0;
				for (int j = 0; j < 8; j++)
				{
					ushort segment2 = data.GetSegment(j);
					if (segment2 != 0)
					{
						if ((instance2.m_segments.m_buffer[(int)segment2].m_flags & NetSegment.Flags.Collapsed) != NetSegment.Flags.None)
						{
							num5++;
						}
						num6 += (int)instance2.m_segments.m_buffer[(int)segment2].m_wetness;
						num7++;
					}
				}
				if (num7 != 0)
				{
					num6 /= num7;
				}
				Color color = this.m_info.m_color;
				if (num5 != 0)
				{
					float num8 = (float)num5 / (float)num7 * 0.5f;
					color.r = color.r * (1f - num8) + num8 * 0.75f;
					color.g = color.g * (1f - num8) + num8 * 0.75f;
					color.b = color.b * (1f - num8) + num8 * 0.75f;
				}
				color.a = (float)(255 - num6) * 0.003921569f;
				return color;
			}
			if (infoMode == InfoManager.InfoMode.Traffic)
			{
				int num9 = 0;
				NetManager instance3 = Singleton<NetManager>.instance;
				int num10 = 0;
				for (int k = 0; k < 8; k++)
				{
					ushort segment3 = data.GetSegment(k);
					if (segment3 != 0)
					{
						num9 += (int)instance3.m_segments.m_buffer[(int)segment3].m_trafficDensity;
						num10++;
					}
				}
				if (num10 != 0)
				{
					num9 /= num10;
				}
				return Color.Lerp(Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_targetColor, Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_negativeColor, Mathf.Clamp01((float)num9 * 0.01f));
			}
			if (infoMode != InfoManager.InfoMode.Destruction)
			{
				return base.GetColor(nodeID, ref data, infoMode, subInfoMode);
			}
			NetManager instance4 = Singleton<NetManager>.instance;
			bool flag = false;
			for (int l = 0; l < 8; l++)
			{
				ushort segment4 = data.GetSegment(l);
				if (segment4 != 0 && (instance4.m_segments.m_buffer[(int)segment4].m_flags & NetSegment.Flags.Collapsed) != NetSegment.Flags.None)
				{
					flag = true;
				}
			}
			if (flag)
			{
				return Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_negativeColor;
			}
			return Singleton<InfoManager>.instance.m_properties.m_neutralColor;
		}
		else
		{
			if (this.m_info.m_class.m_subService == ItemClass.SubService.PublicTransportMonorail)
			{
				return Color.Lerp(Singleton<InfoManager>.instance.m_properties.m_neutralColor, Singleton<TransportManager>.instance.m_properties.m_transportColors[8], 0.2f);
			}
			return Color.Lerp(Singleton<InfoManager>.instance.m_properties.m_neutralColor, Singleton<TransportManager>.instance.m_properties.m_transportColors[2], 0.2f);
		}
	}

	// Token: 0x06003D13 RID: 15635 RVA: 0x00281188 File Offset: 0x0027F588
	public override void GetNodeState(ushort nodeID, ref NetNode nodeData, ushort segmentID, ref NetSegment segmentData, out NetNode.FlagsLong flags, out Color color)
	{
		flags = nodeData.flags;
		color = Color.gray.gamma;
		if ((nodeData.m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None)
		{
			TrainTrackBaseAI.GetLevelCrossingNodeState(nodeID, ref nodeData, segmentID, ref segmentData, ref flags, ref color);
		}
	}

	// Token: 0x06003D14 RID: 15636 RVA: 0x002811D4 File Offset: 0x0027F5D4
	public static void GetLevelCrossingNodeState(ushort nodeID, ref NetNode nodeData, ushort segmentID, ref NetSegment segmentData, ref NetNode.FlagsLong flags, ref Color color)
	{
		uint num = Singleton<SimulationManager>.instance.m_referenceFrameIndex - 15U;
		uint num2 = (uint)(((int)nodeID << 8) / 32768);
		uint num3 = (num - num2) & 255U;
		float num4 = num3 + Singleton<SimulationManager>.instance.m_referenceTimer;
		RoadBaseAI.TrafficLightState trafficLightState;
		RoadBaseAI.TrafficLightState trafficLightState2;
		RoadBaseAI.GetTrafficLightState(nodeID, ref segmentData, num - num2, out trafficLightState, out trafficLightState2);
		color.a = 0.5f;
		switch (trafficLightState)
		{
		case RoadBaseAI.TrafficLightState.Green:
			color.g = 1f;
			color.a = 0.5f;
			break;
		case RoadBaseAI.TrafficLightState.RedToGreen:
			if (num3 < 45U)
			{
				color.g = 0f;
			}
			else if (num3 < 60U)
			{
				color.r = 1f;
			}
			else
			{
				color.g = 1f;
			}
			color.a = 0.5f + Mathf.Clamp((90f - num4) / 180f, 0f, 0.5f);
			break;
		case RoadBaseAI.TrafficLightState.Red:
			color.g = 0f;
			color.a = 1f;
			break;
		case RoadBaseAI.TrafficLightState.GreenToRed:
			if (num3 < 45U)
			{
				color.r = 1f;
			}
			else
			{
				color.g = 0f;
			}
			color.a = 0.5f + Mathf.Clamp(num4 / 180f, 0f, 0.5f);
			break;
		}
		if (Singleton<SimulationManager>.instance.m_metaData.m_invertTraffic == SimulationMetaData.MetaBool.True)
		{
			color.a = 1f - color.a;
		}
	}

	// Token: 0x06003D15 RID: 15637 RVA: 0x0028136B File Offset: 0x0027F76B
	public override void NodeLoaded(ushort nodeID, ref NetNode data, uint version)
	{
		base.NodeLoaded(nodeID, ref data, version);
		Singleton<NetManager>.instance.AddTileNode(data.m_position, this.m_info.m_class.m_service, this.m_info.m_class.m_subService);
	}

	// Token: 0x06003D16 RID: 15638 RVA: 0x002813A8 File Offset: 0x0027F7A8
	public override int RayCastNodeButton(ushort nodeID, ref NetNode data, Segment3 ray)
	{
		if ((data.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None && Singleton<InfoManager>.instance.CurrentMode == InfoManager.InfoMode.TrafficRoutes && Singleton<InfoManager>.instance.CurrentSubMode == InfoManager.SubInfoMode.WaterPower && (this.m_info.m_vehicleTypes & VehicleInfo.VehicleType.Train) != VehicleInfo.VehicleType.None)
		{
			Vector3 position = data.m_position;
			position.y += this.GetSnapElevation();
			float num = Vector3.Distance(ray.a, position);
			if (num < 1000f)
			{
				bool flag = (data.m_flags & NetNode.Flags.OneWayIn) == NetNode.Flags.None;
				float num2 = 0.0675f * Mathf.Pow(num, 0.65f);
				if (flag)
				{
					NetManager instance = Singleton<NetManager>.instance;
					float num3 = this.GetCollisionHalfWidth() * 0.75f;
					for (int i = 0; i < 8; i++)
					{
						ushort segment = data.GetSegment(i);
						if (segment != 0)
						{
							NetInfo info = instance.m_segments.m_buffer[(int)segment].Info;
							bool flag2 = instance.m_segments.m_buffer[(int)segment].m_startNode == nodeID;
							bool flag3 = (instance.m_segments.m_buffer[(int)segment].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;
							if ((flag2 != flag3) ? info.m_hasBackwardVehicleLanes : info.m_hasForwardVehicleLanes)
							{
								Vector3 vector = ((!flag2) ? instance.m_segments.m_buffer[(int)segment].m_endDirection : instance.m_segments.m_buffer[(int)segment].m_startDirection);
								Vector3 vector2 = position + vector * num3;
								if (ray.DistanceSqr(vector2) < num2 * num2)
								{
									return i + 1;
								}
							}
						}
					}
				}
			}
		}
		return 0;
	}

	// Token: 0x06003D17 RID: 15639 RVA: 0x00281574 File Offset: 0x0027F974
	public override void ClickNodeButton(ushort nodeID, ref NetNode data, int index)
	{
		if ((data.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None && Singleton<InfoManager>.instance.CurrentMode == InfoManager.InfoMode.TrafficRoutes && Singleton<InfoManager>.instance.CurrentSubMode == InfoManager.SubInfoMode.WaterPower && (this.m_info.m_vehicleTypes & VehicleInfo.VehicleType.Train) != VehicleInfo.VehicleType.None && index >= 1 && index <= 8 && (data.m_flags & NetNode.Flags.OneWayIn) == NetNode.Flags.None)
		{
			ushort segment = data.GetSegment(index - 1);
			if (segment != 0)
			{
				NetManager instance = Singleton<NetManager>.instance;
				bool flag = instance.m_segments.m_buffer[(int)segment].m_startNode == nodeID;
				NetSegment.Flags flags = ((!flag) ? NetSegment.Flags.YieldEnd : NetSegment.Flags.YieldStart);
				NetSegment[] buffer = instance.m_segments.m_buffer;
				ushort num = segment;
				buffer[(int)num].m_flags = buffer[(int)num].m_flags ^ flags;
				instance.m_segments.m_buffer[(int)segment].UpdateLanes(segment, true);
				Singleton<NetManager>.instance.m_yieldLights.Disable();
			}
		}
	}

	// Token: 0x06003D18 RID: 15640 RVA: 0x00281670 File Offset: 0x0027FA70
	public override void ManualActivation(ushort segmentID, ref NetSegment data, NetInfo oldInfo)
	{
		if (this.m_noiseAccumulation != 0)
		{
			NetManager instance = Singleton<NetManager>.instance;
			Vector3 position = instance.m_nodes.m_buffer[(int)data.m_startNode].m_position;
			Vector3 position2 = instance.m_nodes.m_buffer[(int)data.m_endNode].m_position;
			Vector3 vector = (position + position2) * 0.5f;
			float num = Vector3.Distance(position, position2);
			float num2 = (float)this.m_noiseAccumulation * num / this.m_noiseRadius;
			float num3 = Mathf.Max(num, this.m_noiseRadius);
			if (oldInfo != null)
			{
				int num4;
				float num5;
				oldInfo.m_netAI.GetNoiseAccumulation(out num4, out num5);
				if (num4 != 0)
				{
					num2 -= (float)num4 * num / num5;
					num3 = Mathf.Max(num3, num5);
				}
			}
			if (num2 != 0f)
			{
				Singleton<NotificationManager>.instance.AddWaveEvent(vector, (num2 <= 0f) ? NotificationEvent.Type.Happy : NotificationEvent.Type.Sad, ImmaterialResourceManager.Resource.NoisePollution, num2, num3);
			}
		}
	}

	// Token: 0x06003D19 RID: 15641 RVA: 0x00281768 File Offset: 0x0027FB68
	public override void ManualDeactivation(ushort segmentID, ref NetSegment data)
	{
		if (this.m_noiseAccumulation != 0)
		{
			NetManager instance = Singleton<NetManager>.instance;
			Vector3 position = instance.m_nodes.m_buffer[(int)data.m_startNode].m_position;
			Vector3 position2 = instance.m_nodes.m_buffer[(int)data.m_endNode].m_position;
			Vector3 vector = (position + position2) * 0.5f;
			float num = Vector3.Distance(position, position2);
			float num2 = (float)this.m_noiseAccumulation * num / this.m_noiseRadius;
			float num3 = Mathf.Max(num, this.m_noiseRadius);
			Singleton<NotificationManager>.instance.AddWaveEvent(vector, NotificationEvent.Type.Happy, ImmaterialResourceManager.Resource.NoisePollution, -num2, num3);
		}
	}

	// Token: 0x06003D1A RID: 15642 RVA: 0x0028180D File Offset: 0x0027FC0D
	public override void GetNoiseAccumulation(out int noiseAccumulation, out float noiseRadius)
	{
		noiseAccumulation = this.m_noiseAccumulation;
		noiseRadius = this.m_noiseRadius;
	}

	// Token: 0x06003D1B RID: 15643 RVA: 0x0028181F File Offset: 0x0027FC1F
	public override void TrafficDirectionUpdated(ushort nodeID, ref NetNode data)
	{
		base.TrafficDirectionUpdated(nodeID, ref data);
		this.UpdateOutsideFlags(nodeID, ref data);
	}

	// Token: 0x06003D1C RID: 15644 RVA: 0x00281831 File Offset: 0x0027FC31
	public override void GetNodeBuilding(ushort nodeID, ref NetNode data, out BuildingInfo building, out float heightOffset)
	{
		if ((data.m_flags & NetNode.Flags.Outside) != NetNode.Flags.None)
		{
			building = this.m_outsideConnection;
			heightOffset = -2f;
		}
		else
		{
			base.GetNodeBuilding(nodeID, ref data, out building, out heightOffset);
		}
	}

	// Token: 0x06003D1D RID: 15645 RVA: 0x00281864 File Offset: 0x0027FC64
	public override void SimulationStep(ushort segmentID, ref NetSegment data)
	{
		base.SimulationStep(segmentID, ref data);
		NetManager instance = Singleton<NetManager>.instance;
		Notification.ProblemStruct problemStruct = Notification.RemoveProblems(data.m_problems, Notification.Problem1.Flood);
		float num = 0f;
		uint num2 = data.m_lanes;
		int num3 = 0;
		while (num3 < this.m_info.m_lanes.Length && num2 != 0U)
		{
			NetInfo.Lane lane = this.m_info.m_lanes[num3];
			if (lane.m_laneType == NetInfo.LaneType.Vehicle)
			{
				num += instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_length;
			}
			num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
			num3++;
		}
		int num4 = Mathf.RoundToInt(num) << 4;
		int num5 = 0;
		int num6 = 0;
		if (num4 != 0)
		{
			num5 = (int)((byte)Mathf.Min((int)(data.m_trafficBuffer * 100) / num4, 100));
			num6 = (int)((byte)Mathf.Min((int)(data.m_noiseBuffer * 250) / num4, 100));
		}
		data.m_trafficBuffer = 0;
		data.m_noiseBuffer = 0;
		if (num5 > (int)data.m_trafficDensity)
		{
			data.m_trafficDensity = (byte)Mathf.Min((int)(data.m_trafficDensity + 5), num5);
		}
		else if (num5 < (int)data.m_trafficDensity)
		{
			data.m_trafficDensity = (byte)Mathf.Max((int)(data.m_trafficDensity - 5), num5);
		}
		if (num6 > (int)data.m_noiseDensity)
		{
			data.m_noiseDensity = (byte)Mathf.Min((int)(data.m_noiseDensity + 2), num6);
		}
		else if (num6 < (int)data.m_noiseDensity)
		{
			data.m_noiseDensity = (byte)Mathf.Max((int)(data.m_noiseDensity - 2), num6);
		}
		Vector3 position = instance.m_nodes.m_buffer[(int)data.m_startNode].m_position;
		Vector3 position2 = instance.m_nodes.m_buffer[(int)data.m_endNode].m_position;
		Vector3 vector = (position + position2) * 0.5f;
		bool flag = false;
		if ((this.m_info.m_setVehicleFlags & Vehicle.Flags.Underground) == (Vehicle.Flags)0)
		{
			float num7 = Singleton<TerrainManager>.instance.WaterLevel(VectorUtils.XZ(vector));
			if (num7 > vector.y + 1f && num7 > 0f)
			{
				flag = true;
				data.m_flags |= NetSegment.Flags.Flooded;
				problemStruct = Notification.AddProblems(problemStruct, Notification.Problem1.Flood | Notification.Problem1.MajorProblem);
			}
			else
			{
				data.m_flags &= ~NetSegment.Flags.Flooded;
				if (num7 > vector.y && num7 > 0f)
				{
					flag = true;
					problemStruct = Notification.AddProblems(problemStruct, Notification.Problem1.Flood);
				}
			}
			int num8 = (int)data.m_wetness;
			if (!instance.m_treatWetAsSnow)
			{
				if (flag)
				{
					num8 = 255;
				}
				else
				{
					int num9 = -(num8 + 63 >> 5);
					float num10 = Singleton<WeatherManager>.instance.SampleRainIntensity(vector, false);
					if (num10 != 0f)
					{
						int num11 = Mathf.RoundToInt(Mathf.Min(num10 * 4000f, 1000f));
						num9 += Singleton<SimulationManager>.instance.m_randomizer.Int32(num11, num11 + 99) / 100;
					}
					num8 = Mathf.Clamp(num8 + num9, 0, 255);
				}
			}
			if (num8 != (int)data.m_wetness)
			{
				if (Mathf.Abs((int)data.m_wetness - num8) > 10)
				{
					data.m_wetness = (byte)num8;
					InstanceID empty = InstanceID.Empty;
					empty.NetSegment = segmentID;
					instance.AddSmoothColor(empty);
					empty.NetNode = data.m_startNode;
					instance.AddSmoothColor(empty);
					empty.NetNode = data.m_endNode;
					instance.AddSmoothColor(empty);
				}
				else
				{
					data.m_wetness = (byte)num8;
					instance.m_wetnessChanged = 256;
				}
			}
		}
		int num12 = (int)(100 - (data.m_noiseDensity - 100) * (data.m_noiseDensity - 100) / 100);
		int num13 = this.m_noiseAccumulation * num12 / 100;
		if (num13 != 0)
		{
			float num14 = Vector3.Distance(position, position2);
			int num15 = Mathf.FloorToInt(num14 / this.m_noiseRadius);
			for (int i = 0; i < num15; i++)
			{
				Vector3 vector2 = Vector3.Lerp(position, position2, (float)(i + 1) / (float)(num15 + 1));
				Singleton<ImmaterialResourceManager>.instance.AddResource(ImmaterialResourceManager.Resource.NoisePollution, num13, vector2, this.m_noiseRadius);
			}
		}
		data.m_problems = problemStruct;
	}

	// Token: 0x06003D1E RID: 15646 RVA: 0x00281CC0 File Offset: 0x002800C0
	public override void SimulationStep(ushort nodeID, ref NetNode data)
	{
		base.SimulationStep(nodeID, ref data);
		if ((data.m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None)
		{
			TrainTrackBaseAI.LevelCrossingSimulationStep(nodeID, ref data);
		}
		int num = 0;
		if (this.m_noiseAccumulation != 0)
		{
			NetManager instance = Singleton<NetManager>.instance;
			int num2 = 0;
			for (int i = 0; i < 8; i++)
			{
				ushort segment = data.GetSegment(i);
				if (segment != 0)
				{
					num += (int)instance.m_segments.m_buffer[(int)segment].m_noiseDensity;
					num2++;
				}
			}
			if (num2 != 0)
			{
				num /= num2;
			}
		}
		int num3 = 100 - (num - 100) * (num - 100) / 100;
		int num4 = this.m_noiseAccumulation * num3 / 100;
		if (num4 != 0)
		{
			Singleton<ImmaterialResourceManager>.instance.AddResource(ImmaterialResourceManager.Resource.NoisePollution, num4, data.m_position, this.m_noiseRadius);
		}
	}

	// Token: 0x06003D1F RID: 15647 RVA: 0x00281D8C File Offset: 0x0028018C
	public static void LevelCrossingSimulationStep(ushort nodeID, ref NetNode data)
	{
		NetManager instance = Singleton<NetManager>.instance;
		uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
		bool flag = false;
		for (int i = 0; i < 8; i++)
		{
			ushort segment = data.GetSegment(i);
			if (segment != 0)
			{
				NetInfo info = instance.m_segments.m_buffer[(int)segment].Info;
				if (info.m_class.m_service != ItemClass.Service.Road)
				{
					if (info.m_lanes != null)
					{
						bool flag2 = instance.m_segments.m_buffer[(int)segment].m_startNode == nodeID;
						uint num = instance.m_segments.m_buffer[(int)segment].m_lanes;
						int num2 = 0;
						while (num2 < info.m_lanes.Length && num != 0U)
						{
							if (info.m_lanes[num2].m_laneType == NetInfo.LaneType.Vehicle)
							{
								Vector3 vector = instance.m_lanes.m_buffer[(int)((UIntPtr)num)].CalculatePosition((!flag2) ? 1f : 0f);
								if (TrainTrackBaseAI.CheckOverlap(vector))
								{
									flag = true;
								}
							}
							num = instance.m_lanes.m_buffer[(int)((UIntPtr)num)].m_nextLane;
							num2++;
						}
					}
					RoadBaseAI.TrafficLightState trafficLightState;
					RoadBaseAI.TrafficLightState trafficLightState2;
					bool flag3;
					bool flag4;
					RoadBaseAI.GetTrafficLightState(nodeID, ref instance.m_segments.m_buffer[(int)segment], currentFrameIndex - 256U, out trafficLightState, out trafficLightState2, out flag3, out flag4);
					if (flag3)
					{
						flag = true;
					}
				}
			}
		}
		bool flag5 = flag;
		for (int j = 0; j < 8; j++)
		{
			ushort segment2 = data.GetSegment(j);
			if (segment2 != 0)
			{
				NetInfo info2 = instance.m_segments.m_buffer[(int)segment2].Info;
				RoadBaseAI.TrafficLightState trafficLightState3;
				RoadBaseAI.TrafficLightState trafficLightState4;
				RoadBaseAI.GetTrafficLightState(nodeID, ref instance.m_segments.m_buffer[(int)segment2], currentFrameIndex - 256U, out trafficLightState3, out trafficLightState4);
				trafficLightState3 &= ~RoadBaseAI.TrafficLightState.RedToGreen;
				trafficLightState4 &= ~RoadBaseAI.TrafficLightState.RedToGreen;
				if (info2.m_class.m_service == ItemClass.Service.Road)
				{
					if (flag5)
					{
						if ((trafficLightState3 & RoadBaseAI.TrafficLightState.Red) == RoadBaseAI.TrafficLightState.Green)
						{
							trafficLightState3 = RoadBaseAI.TrafficLightState.GreenToRed;
						}
					}
					else if ((trafficLightState3 & RoadBaseAI.TrafficLightState.Red) != RoadBaseAI.TrafficLightState.Green)
					{
						trafficLightState3 = RoadBaseAI.TrafficLightState.RedToGreen;
					}
					trafficLightState4 = RoadBaseAI.TrafficLightState.Green;
				}
				else if (flag5)
				{
					if ((trafficLightState3 & RoadBaseAI.TrafficLightState.Red) != RoadBaseAI.TrafficLightState.Green)
					{
						trafficLightState3 = RoadBaseAI.TrafficLightState.RedToGreen;
					}
					if ((trafficLightState4 & RoadBaseAI.TrafficLightState.Red) == RoadBaseAI.TrafficLightState.Green)
					{
						trafficLightState4 = RoadBaseAI.TrafficLightState.GreenToRed;
					}
				}
				else
				{
					if ((trafficLightState3 & RoadBaseAI.TrafficLightState.Red) == RoadBaseAI.TrafficLightState.Green)
					{
						trafficLightState3 = RoadBaseAI.TrafficLightState.GreenToRed;
					}
					if ((trafficLightState4 & RoadBaseAI.TrafficLightState.Red) != RoadBaseAI.TrafficLightState.Green)
					{
						trafficLightState4 = RoadBaseAI.TrafficLightState.RedToGreen;
					}
				}
				RoadBaseAI.SetTrafficLightState(nodeID, ref instance.m_segments.m_buffer[(int)segment2], currentFrameIndex, trafficLightState3, trafficLightState4, false, false);
			}
		}
	}

	// Token: 0x06003D20 RID: 15648 RVA: 0x00282010 File Offset: 0x00280410
	public override void UpdateNodeFlags(ushort nodeID, ref NetNode data)
	{
		base.UpdateNodeFlags(nodeID, ref data);
		NetNode.Flags flags = data.m_flags & ~(NetNode.Flags.Transition | NetNode.Flags.LevelCrossing | NetNode.Flags.TrafficLights);
		int num = 0;
		int num2 = 0;
		NetManager instance = Singleton<NetManager>.instance;
		for (int i = 0; i < 8; i++)
		{
			ushort segment = data.GetSegment(i);
			if (segment != 0)
			{
				NetInfo info = instance.m_segments.m_buffer[(int)segment].Info;
				if (info != null)
				{
					if (info.m_createPavement)
					{
						flags |= NetNode.Flags.Transition;
					}
					if (info.m_class.m_service == ItemClass.Service.Road)
					{
						num++;
					}
					else if (info.m_class.m_service == ItemClass.Service.PublicTransport)
					{
						num2++;
					}
				}
			}
		}
		if (num >= 1 && num2 >= 2)
		{
			flags |= NetNode.Flags.LevelCrossing | NetNode.Flags.TrafficLights;
		}
		data.m_flags = flags;
	}

	// Token: 0x06003D21 RID: 15649 RVA: 0x002820E8 File Offset: 0x002804E8
	private static bool CheckOverlap(Vector3 pos)
	{
		VehicleManager instance = Singleton<VehicleManager>.instance;
		int num = Mathf.Max((int)((pos.x - 10f) / 32f + 270f), 0);
		int num2 = Mathf.Max((int)((pos.z - 10f) / 32f + 270f), 0);
		int num3 = Mathf.Min((int)((pos.x + 10f) / 32f + 270f), 539);
		int num4 = Mathf.Min((int)((pos.z + 10f) / 32f + 270f), 539);
		bool flag = false;
		for (int i = num2; i <= num4; i++)
		{
			for (int j = num; j <= num3; j++)
			{
				ushort num5 = instance.m_vehicleGrid[i * 540 + j];
				int num6 = 0;
				while (num5 != 0)
				{
					num5 = TrainTrackBaseAI.CheckOverlap(pos, num5, ref instance.m_vehicles.m_buffer[(int)num5], ref flag);
					if (++num6 > 16384)
					{
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}
			}
		}
		return flag;
	}

	// Token: 0x06003D22 RID: 15650 RVA: 0x00282228 File Offset: 0x00280628
	private static ushort CheckOverlap(Vector3 pos, ushort otherID, ref Vehicle otherData, ref bool overlap)
	{
		float num;
		if (otherData.m_segment.DistanceSqr(pos, ref num) < 4f)
		{
			overlap = true;
		}
		return otherData.m_nextGridVehicle;
	}

	// Token: 0x06003D23 RID: 15651 RVA: 0x00282258 File Offset: 0x00280658
	public override void UpdateNode(ushort nodeID, ref NetNode data)
	{
		base.UpdateNode(nodeID, ref data);
		Notification.ProblemStruct problemStruct = Notification.RemoveProblems(data.m_problems, Notification.Problem1.RoadNotConnected);
		VehicleInfo.VehicleType vehicleType = this.m_info.m_vehicleTypes & ~VehicleInfo.VehicleType.Car;
		VehicleInfo.VehicleCategory vehicleCategories = this.m_info.m_vehicleCategories;
		int num = 0;
		int num2 = 0;
		data.CountLanes(nodeID, 0, NetInfo.LaneType.Vehicle, vehicleType, vehicleCategories, true, ref num, ref num2);
		if ((data.m_flags & NetNode.Flags.Outside) != NetNode.Flags.None && data.m_building != 0)
		{
			BuildingManager instance = Singleton<BuildingManager>.instance;
			if (num != 0)
			{
				Building[] buffer = instance.m_buildings.m_buffer;
				ushort building = data.m_building;
				buffer[(int)building].m_flags = buffer[(int)building].m_flags | Building.Flags.Outgoing;
			}
			else
			{
				Building[] buffer2 = instance.m_buildings.m_buffer;
				ushort building2 = data.m_building;
				buffer2[(int)building2].m_flags = buffer2[(int)building2].m_flags & ~Building.Flags.Outgoing;
			}
			if (num2 != 0)
			{
				Building[] buffer3 = instance.m_buildings.m_buffer;
				ushort building3 = data.m_building;
				buffer3[(int)building3].m_flags = buffer3[(int)building3].m_flags | Building.Flags.Incoming;
			}
			else
			{
				Building[] buffer4 = instance.m_buildings.m_buffer;
				ushort building4 = data.m_building;
				buffer4[(int)building4].m_flags = buffer4[(int)building4].m_flags & ~Building.Flags.Incoming;
			}
		}
		else if ((num != 0 && num2 == 0) || (num == 0 && num2 != 0))
		{
			problemStruct = Notification.AddProblems(problemStruct, Notification.Problem1.RoadNotConnected);
		}
		num = 0;
		num2 = 0;
		data.CountLanes(nodeID, 0, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Car, VehicleInfo.VehicleCategory.All, true, ref num, ref num2);
		if ((num != 0 && num2 == 0) || (num == 0 && num2 != 0))
		{
			problemStruct = Notification.AddProblems(problemStruct, Notification.Problem1.RoadNotConnected);
		}
		data.m_problems = problemStruct;
	}

	// Token: 0x06003D24 RID: 15652 RVA: 0x00282404 File Offset: 0x00280804
	private void UpdateOutsideFlags(ushort nodeID, ref NetNode data)
	{
		if ((data.m_flags & NetNode.Flags.Outside) != NetNode.Flags.None && data.m_building != 0)
		{
			VehicleInfo.VehicleType vehicleType = this.m_info.m_vehicleTypes & ~VehicleInfo.VehicleType.Car;
			VehicleInfo.VehicleCategory vehicleCategories = this.m_info.m_vehicleCategories;
			int num = 0;
			int num2 = 0;
			data.CountLanes(nodeID, 0, NetInfo.LaneType.Vehicle, vehicleType, vehicleCategories, true, ref num, ref num2);
			BuildingManager instance = Singleton<BuildingManager>.instance;
			if (num != 0)
			{
				Building[] buffer = instance.m_buildings.m_buffer;
				ushort building = data.m_building;
				buffer[(int)building].m_flags = buffer[(int)building].m_flags | Building.Flags.Outgoing;
			}
			else
			{
				Building[] buffer2 = instance.m_buildings.m_buffer;
				ushort building2 = data.m_building;
				buffer2[(int)building2].m_flags = buffer2[(int)building2].m_flags & ~Building.Flags.Outgoing;
			}
			if (num2 != 0)
			{
				Building[] buffer3 = instance.m_buildings.m_buffer;
				ushort building3 = data.m_building;
				buffer3[(int)building3].m_flags = buffer3[(int)building3].m_flags | Building.Flags.Incoming;
			}
			else
			{
				Building[] buffer4 = instance.m_buildings.m_buffer;
				ushort building4 = data.m_building;
				buffer4[(int)building4].m_flags = buffer4[(int)building4].m_flags & ~Building.Flags.Incoming;
			}
		}
	}

	// Token: 0x06003D25 RID: 15653 RVA: 0x00282514 File Offset: 0x00280914
	public override void UpdateLanes(ushort segmentID, ref NetSegment data, bool loading)
	{
		base.UpdateLanes(segmentID, ref data, loading);
		if (!loading)
		{
			NetManager instance = Singleton<NetManager>.instance;
			int num = Mathf.Max((int)((data.m_bounds.min.x - 16f) / 64f + 135f), 0);
			int num2 = Mathf.Max((int)((data.m_bounds.min.z - 16f) / 64f + 135f), 0);
			int num3 = Mathf.Min((int)((data.m_bounds.max.x + 16f) / 64f + 135f), 269);
			int num4 = Mathf.Min((int)((data.m_bounds.max.z + 16f) / 64f + 135f), 269);
			for (int i = num2; i <= num4; i++)
			{
				for (int j = num; j <= num3; j++)
				{
					ushort num5 = instance.m_nodeGrid[i * 270 + j];
					int num6 = 0;
					while (num5 != 0)
					{
						NetInfo info = instance.m_nodes.m_buffer[(int)num5].Info;
						Vector3 position = instance.m_nodes.m_buffer[(int)num5].m_position;
						float num7 = Mathf.Max(Mathf.Max(data.m_bounds.min.x - 16f - position.x, data.m_bounds.min.z - 16f - position.z), Mathf.Max(position.x - data.m_bounds.max.x - 16f, position.z - data.m_bounds.max.z - 16f));
						if (num7 < 0f)
						{
							info.m_netAI.NearbyLanesUpdated(num5, ref instance.m_nodes.m_buffer[(int)num5]);
						}
						num5 = instance.m_nodes.m_buffer[(int)num5].m_nextGridNode;
						if (++num6 >= 32768)
						{
							CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
							break;
						}
					}
				}
			}
		}
	}

	// Token: 0x06003D26 RID: 15654 RVA: 0x00282784 File Offset: 0x00280B84
	public override ToolBase.ToolErrors CanConnectTo(ushort node, ushort segment, ulong[] segmentMask)
	{
		ToolBase.ToolErrors toolErrors = base.CanConnectTo(node, segment, segmentMask);
		NetManager instance = Singleton<NetManager>.instance;
		if (node != 0)
		{
			for (int i = 0; i < 8; i++)
			{
				ushort segment2 = instance.m_nodes.m_buffer[(int)node].GetSegment(i);
				if (segment2 != 0)
				{
					NetInfo info = instance.m_segments.m_buffer[(int)segment2].Info;
					if (info.m_class.m_service == ItemClass.Service.Road && info.m_netAI.IsOverground())
					{
						toolErrors |= ToolBase.ToolErrors.ObjectCollision;
						if (segmentMask != null)
						{
							segmentMask[segment2 >> 6] |= 1UL << (int)segment2;
						}
					}
				}
			}
		}
		else if (segment != 0)
		{
			NetInfo info2 = instance.m_segments.m_buffer[(int)segment].Info;
			if (info2.m_class.m_service == ItemClass.Service.Road && info2.m_netAI.IsOverground())
			{
				toolErrors |= ToolBase.ToolErrors.ObjectCollision;
				if (segmentMask != null)
				{
					segmentMask[segment >> 6] |= 1UL << (int)segment;
				}
			}
		}
		return toolErrors;
	}

	// Token: 0x06003D27 RID: 15655 RVA: 0x002828A4 File Offset: 0x00280CA4
	public override bool CollapseSegment(ushort segmentID, ref NetSegment data, InstanceManager.Group group, bool demolish)
	{
		if (demolish)
		{
			return base.CollapseSegment(segmentID, ref data, group, demolish);
		}
		if ((data.m_flags & NetSegment.Flags.Untouchable) != NetSegment.Flags.None)
		{
			return false;
		}
		if ((data.m_flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted | NetSegment.Flags.Collapsed)) != NetSegment.Flags.Created)
		{
			return false;
		}
		if (DefaultTool.IsOutOfCityArea(segmentID))
		{
			return false;
		}
		data.m_flags |= NetSegment.Flags.Collapsed;
		if (group != null)
		{
			ushort disaster = group.m_ownerInstance.Disaster;
			if (disaster != 0)
			{
				DisasterData[] buffer = Singleton<DisasterManager>.instance.m_disasters.m_buffer;
				ushort num = disaster;
				buffer[(int)num].m_destroyedTrackLength = buffer[(int)num].m_destroyedTrackLength + (uint)Mathf.RoundToInt(data.m_averageLength);
			}
		}
		Notification.ProblemStruct problems = data.m_problems;
		data.m_problems = Notification.Problem1.StructureDamaged | Notification.Problem1.FatalProblem;
		if (data.m_problems != problems)
		{
			Singleton<NetManager>.instance.UpdateSegmentNotifications(segmentID, problems, data.m_problems);
		}
		Singleton<NetManager>.instance.UpdateSegmentRenderer(segmentID, true);
		Singleton<NetManager>.instance.UpdateSegmentColors(segmentID);
		return true;
	}

	// Token: 0x06003D28 RID: 15656 RVA: 0x0028299C File Offset: 0x00280D9C
	public override void ParentCollapsed(ushort segmentID, ref NetSegment data, InstanceManager.Group group)
	{
		if ((data.m_flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted | NetSegment.Flags.Collapsed)) != NetSegment.Flags.Created)
		{
			return;
		}
		data.m_flags |= NetSegment.Flags.Collapsed;
		if (group != null)
		{
			ushort disaster = group.m_ownerInstance.Disaster;
			if (disaster != 0)
			{
				DisasterData[] buffer = Singleton<DisasterManager>.instance.m_disasters.m_buffer;
				ushort num = disaster;
				buffer[(int)num].m_destroyedTrackLength = buffer[(int)num].m_destroyedTrackLength + (uint)Mathf.RoundToInt(data.m_averageLength);
			}
		}
		Singleton<NetManager>.instance.UpdateSegmentRenderer(segmentID, true);
		Singleton<NetManager>.instance.UpdateSegmentColors(segmentID);
	}

	// Token: 0x06003D29 RID: 15657 RVA: 0x00282A24 File Offset: 0x00280E24
	public override void UpdateSegmentFlags(ushort segmentID, ref NetSegment data)
	{
		base.UpdateSegmentFlags(segmentID, ref data);
		if ((data.m_flags & (NetSegment.Flags.Collapsed | NetSegment.Flags.Untouchable)) == NetSegment.Flags.Collapsed)
		{
			Notification.ProblemStruct problems = data.m_problems;
			data.m_problems = Notification.Problem1.StructureDamaged | Notification.Problem1.FatalProblem;
			if (data.m_problems != problems)
			{
				Singleton<NetManager>.instance.UpdateSegmentNotifications(segmentID, problems, data.m_problems);
			}
		}
	}

	// Token: 0x06003D2A RID: 15658 RVA: 0x00282A88 File Offset: 0x00280E88
	public override Color32 GetGroupVertexColor(NetInfo.Segment segmentInfo, int vertexIndex)
	{
		RenderGroup.MeshData data = segmentInfo.m_combinedLod.m_key.m_mesh.m_data;
		Vector3 vector = data.m_vertices[vertexIndex];
		vector.x = vector.x * 0.5f / this.m_info.m_halfWidth + 0.5f;
		vector.z = vector.z / this.m_info.m_segmentLength + 0.5f;
		Color32 color;
		if (data.m_colors != null && data.m_colors.Length > vertexIndex)
		{
			color = data.m_colors[vertexIndex];
		}
		else
		{
			color..ctor(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
		}
		Vector3 vector2;
		if (data.m_normals != null && data.m_normals.Length > vertexIndex)
		{
			vector2 = data.m_normals[vertexIndex];
		}
		else
		{
			vector2 = Vector3.up;
		}
		color.b = (byte)Mathf.RoundToInt(vector.z * 255f);
		if (segmentInfo.m_requireSurfaceMaps)
		{
			if (vector.y > -0.31f && vector2.y < 0.8f && vector.x > 0.01f && vector.x < 0.99f)
			{
				color.g = byte.MaxValue;
			}
			else
			{
				color.g = 0;
			}
			color.a = byte.MaxValue;
		}
		else
		{
			if (vector2.y > -0.5f)
			{
				color.g = byte.MaxValue;
			}
			else
			{
				color.g = 0;
			}
			color.a = 128;
		}
		return color;
	}

	// Token: 0x06003D2B RID: 15659 RVA: 0x00282C50 File Offset: 0x00281050
	public override Color32 GetGroupVertexColor(NetInfo.Node nodeInfo, int vertexIndex, float vOffset)
	{
		RenderGroup.MeshData data = nodeInfo.m_combinedLod.m_key.m_mesh.m_data;
		Vector3 vector = data.m_vertices[vertexIndex];
		vector.x = vector.x * 0.5f / this.m_info.m_halfWidth + 0.5f;
		Color32 color;
		if (data.m_colors != null && data.m_colors.Length > vertexIndex)
		{
			color = data.m_colors[vertexIndex];
		}
		else
		{
			color..ctor(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
		}
		Vector3 vector2;
		if (data.m_normals != null && data.m_normals.Length > vertexIndex)
		{
			vector2 = data.m_normals[vertexIndex];
		}
		else
		{
			vector2 = Vector3.up;
		}
		color.b = (byte)Mathf.Clamp(Mathf.RoundToInt(vOffset * 255f), 0, 255);
		if (nodeInfo.m_requireSurfaceMaps)
		{
			if (vector.y > -0.31f && vector2.y < 0.8f && vector.x > 0.01f && vector.x < 0.99f)
			{
				color.g = byte.MaxValue;
			}
			else
			{
				color.g = 0;
			}
			color.a = byte.MaxValue;
		}
		else
		{
			if (vector2.y > -0.5f)
			{
				color.g = byte.MaxValue;
			}
			else
			{
				color.g = 0;
			}
			color.a = 128;
		}
		return color;
	}

	// Token: 0x06003D2C RID: 15660 RVA: 0x00282DFB File Offset: 0x002811FB
	public override void GetRayCastHeights(ushort segmentID, ref NetSegment data, out float leftMin, out float rightMin, out float max)
	{
		leftMin = this.m_info.m_minHeight;
		rightMin = this.m_info.m_minHeight;
		max = 0f;
	}

	// Token: 0x04004399 RID: 17305
	[CustomizableProperty("Noise Accumulation", "Properties")]
	public int m_noiseAccumulation = 10;

	// Token: 0x0400439A RID: 17306
	[CustomizableProperty("Noise Radius", "Properties")]
	public float m_noiseRadius = 40f;

	// Token: 0x0400439B RID: 17307
	public BuildingInfo m_outsideConnection;
}
