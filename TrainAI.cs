using System;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

// Token: 0x02000687 RID: 1671
public class TrainAI : VehicleAI
{
	// Token: 0x06004DB7 RID: 19895 RVA: 0x0035B2F8 File Offset: 0x003596F8
	public override Color GetColor(ushort vehicleID, ref Vehicle data, InfoManager.InfoMode infoMode, InfoManager.SubInfoMode subInfoMode)
	{
		if (data.m_leadingVehicle != 0 && infoMode != InfoManager.InfoMode.None)
		{
			VehicleManager instance = Singleton<VehicleManager>.instance;
			ushort firstVehicle = data.GetFirstVehicle(vehicleID);
			VehicleInfo info = instance.m_vehicles.m_buffer[(int)firstVehicle].Info;
			return info.m_vehicleAI.GetColor(firstVehicle, ref instance.m_vehicles.m_buffer[(int)firstVehicle], infoMode, subInfoMode);
		}
		if (infoMode == InfoManager.InfoMode.NoisePollution)
		{
			int noiseLevel = this.GetNoiseLevel();
			return CommonBuildingAI.GetNoisePollutionColor((float)noiseLevel * 2.5f);
		}
		return base.GetColor(vehicleID, ref data, infoMode, subInfoMode);
	}

	// Token: 0x06004DB8 RID: 19896 RVA: 0x0035B384 File Offset: 0x00359784
	public override void LoadVehicle(ushort vehicleID, ref Vehicle data)
	{
		base.LoadVehicle(vehicleID, ref data);
		if (data.m_leadingVehicle == 0 && data.m_trailingVehicle != 0 && (data.m_flags & Vehicle.Flags.Reversed) != (Vehicle.Flags)0)
		{
			VehicleManager instance = Singleton<VehicleManager>.instance;
			ushort num = data.m_trailingVehicle;
			int num2 = 0;
			while (num != 0)
			{
				ushort trailingVehicle = instance.m_vehicles.m_buffer[(int)num].m_trailingVehicle;
				Vehicle[] buffer = instance.m_vehicles.m_buffer;
				ushort num3 = num;
				buffer[(int)num3].m_flags = buffer[(int)num3].m_flags | Vehicle.Flags.Reversed;
				num = trailingVehicle;
				if (++num2 > 16384)
				{
					CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
					break;
				}
			}
		}
	}

	// Token: 0x06004DB9 RID: 19897 RVA: 0x0035B440 File Offset: 0x00359840
	public override void SimulationStep(ushort vehicleID, ref Vehicle data, Vector3 physicsLodRefPos)
	{
		if ((data.m_flags & Vehicle.Flags.WaitingPath) != (Vehicle.Flags)0)
		{
			byte pathFindFlags = Singleton<PathManager>.instance.m_pathUnits.m_buffer[(int)((UIntPtr)data.m_path)].m_pathFindFlags;
			if ((pathFindFlags & 4) != 0)
			{
				this.PathFindReady(vehicleID, ref data);
			}
			else if ((pathFindFlags & 8) != 0 || data.m_path == 0U)
			{
				data.m_flags &= ~Vehicle.Flags.WaitingPath;
				Singleton<PathManager>.instance.ReleasePath(data.m_path);
				data.m_path = 0U;
				data.Unspawn(vehicleID);
				return;
			}
		}
		else if ((data.m_flags & Vehicle.Flags.WaitingSpace) != (Vehicle.Flags)0)
		{
			this.TrySpawn(vehicleID, ref data);
		}
		bool flag = (data.m_flags & Vehicle.Flags.Reversed) != (Vehicle.Flags)0;
		ushort num;
		if (flag)
		{
			num = data.GetLastVehicle(vehicleID);
		}
		else
		{
			num = vehicleID;
		}
		VehicleManager instance = Singleton<VehicleManager>.instance;
		VehicleInfo vehicleInfo = instance.m_vehicles.m_buffer[(int)num].Info;
		vehicleInfo.m_vehicleAI.SimulationStep(num, ref instance.m_vehicles.m_buffer[(int)num], vehicleID, ref data, 0);
		if ((data.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created)
		{
			return;
		}
		bool flag2 = (data.m_flags & Vehicle.Flags.Reversed) != (Vehicle.Flags)0;
		if (flag2 != flag)
		{
			flag = flag2;
			if (flag)
			{
				num = data.GetLastVehicle(vehicleID);
			}
			else
			{
				num = vehicleID;
			}
			vehicleInfo = instance.m_vehicles.m_buffer[(int)num].Info;
			vehicleInfo.m_vehicleAI.SimulationStep(num, ref instance.m_vehicles.m_buffer[(int)num], vehicleID, ref data, 0);
			if ((data.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created)
			{
				return;
			}
			flag2 = (data.m_flags & Vehicle.Flags.Reversed) != (Vehicle.Flags)0;
			if (flag2 != flag)
			{
				Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleID);
				return;
			}
		}
		if (flag)
		{
			num = instance.m_vehicles.m_buffer[(int)num].m_leadingVehicle;
			int num2 = 0;
			while (num != 0)
			{
				vehicleInfo = instance.m_vehicles.m_buffer[(int)num].Info;
				vehicleInfo.m_vehicleAI.SimulationStep(num, ref instance.m_vehicles.m_buffer[(int)num], vehicleID, ref data, 0);
				if ((data.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created)
				{
					return;
				}
				num = instance.m_vehicles.m_buffer[(int)num].m_leadingVehicle;
				if (++num2 > 16384)
				{
					CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
					break;
				}
			}
		}
		else
		{
			num = instance.m_vehicles.m_buffer[(int)num].m_trailingVehicle;
			int num3 = 0;
			while (num != 0)
			{
				vehicleInfo = instance.m_vehicles.m_buffer[(int)num].Info;
				vehicleInfo.m_vehicleAI.SimulationStep(num, ref instance.m_vehicles.m_buffer[(int)num], vehicleID, ref data, 0);
				if ((data.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created)
				{
					return;
				}
				num = instance.m_vehicles.m_buffer[(int)num].m_trailingVehicle;
				if (++num3 > 16384)
				{
					CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
					break;
				}
			}
		}
		if ((data.m_flags & (Vehicle.Flags.Spawned | Vehicle.Flags.WaitingPath | Vehicle.Flags.WaitingSpace | Vehicle.Flags.WaitingCargo)) == (Vehicle.Flags)0 || data.m_blockCounter == 255)
		{
			Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleID);
		}
	}

	// Token: 0x06004DBA RID: 19898 RVA: 0x0035B7A8 File Offset: 0x00359BA8
	public override void SimulationStep(ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ushort leaderID, ref Vehicle leaderData, int lodPhysics)
	{
		bool flag = (leaderData.m_flags & Vehicle.Flags.Reversed) != (Vehicle.Flags)0;
		ushort num = ((!flag) ? vehicleData.m_leadingVehicle : vehicleData.m_trailingVehicle);
		VehicleInfo vehicleInfo;
		if (leaderID != vehicleID)
		{
			vehicleInfo = leaderData.Info;
		}
		else
		{
			vehicleInfo = this.m_info;
		}
		TrainAI trainAI = vehicleInfo.m_vehicleAI as TrainAI;
		if (num != 0)
		{
			frameData.m_position += frameData.m_velocity * 0.4f;
		}
		else
		{
			frameData.m_position += frameData.m_velocity * 0.5f;
		}
		frameData.m_swayPosition += frameData.m_swayVelocity * 0.5f;
		Vector3 vector = frameData.m_position;
		Vector3 vector2 = frameData.m_position;
		Vector3 vector3 = frameData.m_rotation * new Vector3(0f, 0f, this.m_info.m_generatedInfo.m_wheelBase * 0.5f);
		if (flag)
		{
			vector -= vector3;
			vector2 += vector3;
		}
		else
		{
			vector += vector3;
			vector2 -= vector3;
		}
		float acceleration = this.m_info.m_acceleration;
		float braking = this.m_info.m_braking;
		float magnitude = frameData.m_velocity.magnitude;
		Vector3 vector4 = vehicleData.m_targetPos1 - vector;
		float num2 = vector4.sqrMagnitude;
		Quaternion quaternion = Quaternion.Inverse(frameData.m_rotation);
		Vector3 vector5 = quaternion * frameData.m_velocity;
		Vector3 vector6 = Vector3.forward;
		Vector3 vector7 = Vector3.zero;
		float num3 = 0f;
		float num4 = 0.5f;
		if (num != 0)
		{
			VehicleManager instance = Singleton<VehicleManager>.instance;
			Vehicle.Frame lastFrameData = instance.m_vehicles.m_buffer[(int)num].GetLastFrameData();
			VehicleInfo info = instance.m_vehicles.m_buffer[(int)num].Info;
			float num5;
			if ((vehicleData.m_flags & Vehicle.Flags.Inverted) != (Vehicle.Flags)0 != flag)
			{
				num5 = this.m_info.m_attachOffsetBack - this.m_info.m_generatedInfo.m_size.z * 0.5f;
			}
			else
			{
				num5 = this.m_info.m_attachOffsetFront - this.m_info.m_generatedInfo.m_size.z * 0.5f;
			}
			float num6;
			if ((instance.m_vehicles.m_buffer[(int)num].m_flags & Vehicle.Flags.Inverted) != (Vehicle.Flags)0 != flag)
			{
				num6 = info.m_attachOffsetFront - info.m_generatedInfo.m_size.z * 0.5f;
			}
			else
			{
				num6 = info.m_attachOffsetBack - info.m_generatedInfo.m_size.z * 0.5f;
			}
			Vector3 vector8 = frameData.m_position;
			if (flag)
			{
				vector8 += frameData.m_rotation * new Vector3(0f, 0f, num5);
			}
			else
			{
				vector8 -= frameData.m_rotation * new Vector3(0f, 0f, num5);
			}
			Vector3 vector9 = lastFrameData.m_position;
			if (flag)
			{
				vector9 -= lastFrameData.m_rotation * new Vector3(0f, 0f, num6);
			}
			else
			{
				vector9 += lastFrameData.m_rotation * new Vector3(0f, 0f, num6);
			}
			Vector3 vector10 = lastFrameData.m_position;
			vector3 = lastFrameData.m_rotation * new Vector3(0f, 0f, info.m_generatedInfo.m_wheelBase * 0.5f);
			if (flag)
			{
				vector10 += vector3;
			}
			else
			{
				vector10 -= vector3;
			}
			if (Vector3.Dot(vehicleData.m_targetPos1 - vehicleData.m_targetPos0, vehicleData.m_targetPos0 - vector2) < 0f && vehicleData.m_path != 0U && (leaderData.m_flags & Vehicle.Flags.WaitingPath) == (Vehicle.Flags)0)
			{
				int num7 = -1;
				trainAI.UpdatePathTargetPositions(vehicleID, ref vehicleData, vehicleData.m_targetPos0, vector2, 0, ref leaderData, ref num7, 0, 0, Vector3.SqrMagnitude(vector2 - vehicleData.m_targetPos0) + 1f, 1f);
				num2 = 0f;
			}
			float num8 = Mathf.Max(Vector3.Distance(vector8, vector9), 2f);
			float num9 = 1f;
			float num10 = num8 * num8;
			float num11 = num9 * num9;
			int i = 0;
			if (num2 < num10)
			{
				if (vehicleData.m_path != 0U && (leaderData.m_flags & Vehicle.Flags.WaitingPath) == (Vehicle.Flags)0)
				{
					trainAI.UpdatePathTargetPositions(vehicleID, ref vehicleData, vector2, vector, 0, ref leaderData, ref i, 1, 2, num10, num11);
				}
				while (i < 4)
				{
					vehicleData.SetTargetPos(i, vehicleData.GetTargetPos(i - 1));
					i++;
				}
				vector4 = vehicleData.m_targetPos1 - vector;
				num2 = vector4.sqrMagnitude;
			}
			if (vehicleData.m_path != 0U)
			{
				NetManager instance2 = Singleton<NetManager>.instance;
				byte b = vehicleData.m_pathPositionIndex;
				byte lastPathOffset = vehicleData.m_lastPathOffset;
				if (b == 255)
				{
					b = 0;
				}
				PathManager instance3 = Singleton<PathManager>.instance;
				PathUnit.Position position;
				if (instance3.m_pathUnits.m_buffer[(int)((UIntPtr)vehicleData.m_path)].GetPosition(b >> 1, out position))
				{
					instance2.m_segments.m_buffer[(int)position.m_segment].AddTraffic(Mathf.RoundToInt(this.m_info.m_generatedInfo.m_size.z * 3f), this.GetNoiseLevel());
					if ((b & 1) == 0 || lastPathOffset == 0 || (leaderData.m_flags & Vehicle.Flags.WaitingPath) != (Vehicle.Flags)0)
					{
						uint laneID = PathManager.GetLaneID(position);
						if (laneID != 0U)
						{
							instance2.m_lanes.m_buffer[(int)((UIntPtr)laneID)].ReserveSpace(this.m_info.m_generatedInfo.m_size.z);
						}
					}
					else if (instance3.m_pathUnits.m_buffer[(int)((UIntPtr)vehicleData.m_path)].GetNextPosition(b >> 1, out position))
					{
						uint laneID2 = PathManager.GetLaneID(position);
						if (laneID2 != 0U)
						{
							instance2.m_lanes.m_buffer[(int)((UIntPtr)laneID2)].ReserveSpace(this.m_info.m_generatedInfo.m_size.z);
						}
					}
				}
			}
			vector4 = quaternion * vector4;
			float num12 = (this.m_info.m_generatedInfo.m_wheelBase + info.m_generatedInfo.m_wheelBase) * -0.5f - num5 - num6;
			bool flag2 = false;
			if (vehicleData.m_path != 0U && (leaderData.m_flags & Vehicle.Flags.WaitingPath) == (Vehicle.Flags)0)
			{
				float num13;
				float num14;
				if (Line3.Intersect(vector, vehicleData.m_targetPos1, vector10, num12, ref num13, ref num14))
				{
					vector7 = vector4 * Mathf.Clamp(Mathf.Min(num13, num14) / 0.6f, 0f, 2f);
				}
				else
				{
					Line3.DistanceSqr(vector, vehicleData.m_targetPos1, vector10, ref num13);
					vector7 = vector4 * Mathf.Clamp(num13 / 0.6f, 0f, 2f);
				}
				flag2 = true;
			}
			if (flag2)
			{
				if (Vector3.Dot(vector10 - vector, vector - vector2) < 0f)
				{
					num4 = 0f;
				}
			}
			else
			{
				float num15 = Vector3.Distance(vector10, vector);
				num4 = 0f;
				vector7 = quaternion * ((vector10 - vector) * (Mathf.Max(0f, num15 - num12) / Mathf.Max(1f, num15 * 0.6f)));
			}
		}
		else
		{
			float num16 = (magnitude + acceleration) * (0.5f + 0.5f * (magnitude + acceleration) / braking);
			float num17 = Mathf.Max(magnitude + acceleration, 2f);
			float num18 = Mathf.Max((num16 - num17) / 2f, 1f);
			float num19 = num17 * num17;
			float num20 = num18 * num18;
			if (Vector3.Dot(vehicleData.m_targetPos1 - vehicleData.m_targetPos0, vehicleData.m_targetPos0 - vector2) < 0f && vehicleData.m_path != 0U && (leaderData.m_flags & (Vehicle.Flags.WaitingPath | Vehicle.Flags.Stopped)) == (Vehicle.Flags)0)
			{
				int num21 = -1;
				trainAI.UpdatePathTargetPositions(vehicleID, ref vehicleData, vehicleData.m_targetPos0, vector2, leaderID, ref leaderData, ref num21, 0, 0, Vector3.SqrMagnitude(vector2 - vehicleData.m_targetPos0) + 1f, 1f);
				num2 = 0f;
			}
			int j = 0;
			bool flag3 = false;
			if ((num2 < num19 || vehicleData.m_targetPos3.w < 0.01f) && (leaderData.m_flags & (Vehicle.Flags.WaitingPath | Vehicle.Flags.Stopped)) == (Vehicle.Flags)0)
			{
				if (vehicleData.m_path != 0U)
				{
					trainAI.UpdatePathTargetPositions(vehicleID, ref vehicleData, vector2, vector, leaderID, ref leaderData, ref j, 1, 4, num19, num20);
				}
				if (j < 4)
				{
					flag3 = true;
					while (j < 4)
					{
						vehicleData.SetTargetPos(j, vehicleData.GetTargetPos(j - 1));
						j++;
					}
				}
				vector4 = vehicleData.m_targetPos1 - vector;
				num2 = vector4.sqrMagnitude;
			}
			if ((leaderData.m_flags & (Vehicle.Flags.WaitingPath | Vehicle.Flags.Stopped)) == (Vehicle.Flags)0 && this.m_info.m_vehicleType != VehicleInfo.VehicleType.Monorail)
			{
				this.ForceTrafficLights(vehicleID, ref vehicleData, magnitude > 0.1f);
			}
			if (vehicleData.m_path != 0U)
			{
				NetManager instance4 = Singleton<NetManager>.instance;
				byte b2 = vehicleData.m_pathPositionIndex;
				byte lastPathOffset2 = vehicleData.m_lastPathOffset;
				if (b2 == 255)
				{
					b2 = 0;
				}
				PathManager instance5 = Singleton<PathManager>.instance;
				PathUnit.Position position2;
				if (instance5.m_pathUnits.m_buffer[(int)((UIntPtr)vehicleData.m_path)].GetPosition(b2 >> 1, out position2))
				{
					instance4.m_segments.m_buffer[(int)position2.m_segment].AddTraffic(Mathf.RoundToInt(this.m_info.m_generatedInfo.m_size.z * 3f), this.GetNoiseLevel());
					if ((b2 & 1) == 0 || lastPathOffset2 == 0 || (leaderData.m_flags & Vehicle.Flags.WaitingPath) != (Vehicle.Flags)0)
					{
						uint laneID3 = PathManager.GetLaneID(position2);
						if (laneID3 != 0U)
						{
							instance4.m_lanes.m_buffer[(int)((UIntPtr)laneID3)].ReserveSpace(this.m_info.m_generatedInfo.m_size.z, vehicleID);
						}
					}
					else if (instance5.m_pathUnits.m_buffer[(int)((UIntPtr)vehicleData.m_path)].GetNextPosition(b2 >> 1, out position2))
					{
						uint laneID4 = PathManager.GetLaneID(position2);
						if (laneID4 != 0U)
						{
							instance4.m_lanes.m_buffer[(int)((UIntPtr)laneID4)].ReserveSpace(this.m_info.m_generatedInfo.m_size.z, vehicleID);
						}
					}
				}
			}
			float num22;
			if ((leaderData.m_flags & Vehicle.Flags.Stopped) != (Vehicle.Flags)0)
			{
				num22 = 0f;
			}
			else
			{
				num22 = Mathf.Min(vehicleData.m_targetPos1.w, TrainAI.GetMaxSpeed(leaderID, ref leaderData));
			}
			vector4 = quaternion * vector4;
			if (flag)
			{
				vector4 = -vector4;
			}
			bool flag4 = false;
			float num23 = 0f;
			if (num2 > 1f)
			{
				vector6 = VectorUtils.NormalizeXZ(vector4, ref num23);
				if (num23 > 1f)
				{
					Vector3 vector11 = vector4;
					num17 = Mathf.Max(magnitude, 2f);
					num19 = num17 * num17;
					if (num2 > num19)
					{
						float num24 = num17 / Mathf.Sqrt(num2);
						vector11.x *= num24;
						vector11.y *= num24;
					}
					if (vector11.z < -1f)
					{
						if (vehicleData.m_path != 0U && (leaderData.m_flags & Vehicle.Flags.WaitingPath) == (Vehicle.Flags)0)
						{
							Vector3 vector12 = vehicleData.m_targetPos1 - vehicleData.m_targetPos0;
							vector12 = quaternion * vector12;
							if (flag)
							{
								vector12 = -vector12;
							}
							if (vector12.z < -0.01f)
							{
								if (vector4.z < Mathf.Abs(vector4.x) * -10f)
								{
									if (magnitude < 0.01f)
									{
										TrainAI.Reverse(leaderID, ref leaderData);
										return;
									}
									vector11.z = 0f;
									vector4 = Vector3.zero;
									num22 = 0f;
								}
								else
								{
									vector = vector2 + Vector3.Normalize(vehicleData.m_targetPos1 - vehicleData.m_targetPos0) * this.m_info.m_generatedInfo.m_wheelBase;
									j = -1;
									trainAI.UpdatePathTargetPositions(vehicleID, ref vehicleData, vehicleData.m_targetPos0, vehicleData.m_targetPos1, leaderID, ref leaderData, ref j, 0, 0, Vector3.SqrMagnitude(vehicleData.m_targetPos1 - vehicleData.m_targetPos0) + 1f, 1f);
								}
							}
							else
							{
								j = -1;
								trainAI.UpdatePathTargetPositions(vehicleID, ref vehicleData, vehicleData.m_targetPos0, vector2, leaderID, ref leaderData, ref j, 0, 0, Vector3.SqrMagnitude(vector2 - vehicleData.m_targetPos0) + 1f, 1f);
								vehicleData.m_targetPos1 = vector;
								vector11.z = 0f;
								vector4 = Vector3.zero;
								num22 = 0f;
							}
						}
						num4 = 0f;
					}
					vector6 = VectorUtils.NormalizeXZ(vector11, ref num23);
					float num25 = 1.5707964f * (1f - vector6.z);
					if (num23 > 1f)
					{
						num25 /= num23;
					}
					num22 = Mathf.Min(num22, this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1000f, num25));
					float num26 = num23;
					num22 = Mathf.Min(num22, TrainAI.CalculateMaxSpeed(num26, vehicleData.m_targetPos2.w, braking));
					num26 += VectorUtils.LengthXZ(vehicleData.m_targetPos2 - vehicleData.m_targetPos1);
					num22 = Mathf.Min(num22, TrainAI.CalculateMaxSpeed(num26, vehicleData.m_targetPos3.w, braking));
					num26 += VectorUtils.LengthXZ(vehicleData.m_targetPos3 - vehicleData.m_targetPos2);
					num22 = Mathf.Min(num22, TrainAI.CalculateMaxSpeed(num26, 0f, braking));
					if (num22 < magnitude)
					{
						float num27 = Mathf.Max(acceleration, Mathf.Min(braking, magnitude));
						num3 = Mathf.Max(num22, magnitude - num27);
					}
					else
					{
						float num28 = Mathf.Max(acceleration, Mathf.Min(braking, -magnitude));
						num3 = Mathf.Min(num22, magnitude + num28);
					}
				}
			}
			else if (magnitude < 0.1f && flag3 && vehicleInfo.m_vehicleAI.ArriveAtDestination(leaderID, ref leaderData))
			{
				leaderData.Unspawn(leaderID);
				return;
			}
			if ((leaderData.m_flags & Vehicle.Flags.Stopped) == (Vehicle.Flags)0 && num22 < 0.1f)
			{
				flag4 = true;
			}
			if (flag4)
			{
				leaderData.m_blockCounter = (byte)Mathf.Min((int)(leaderData.m_blockCounter + 1), 255);
			}
			else
			{
				leaderData.m_blockCounter = 0;
			}
			if (num23 > 1f)
			{
				if (flag)
				{
					vector6 = -vector6;
				}
				vector7 = vector6 * num3;
			}
			else
			{
				if (flag)
				{
					vector4 = -vector4;
				}
				Vector3 vector13 = Vector3.ClampMagnitude(vector4 * 0.5f - vector5, braking);
				vector7 = vector5 + vector13;
			}
		}
		Vector3 vector14 = vector7 - vector5;
		Vector3 vector15 = frameData.m_rotation * vector7;
		Vector3 vector16 = Vector3.Normalize(vehicleData.m_targetPos0 - vector2) * (vector7.magnitude * num4);
		vector += vector15;
		vector2 += vector16;
		Vector3 vector17;
		if (flag)
		{
			frameData.m_rotation = Quaternion.LookRotation(vector2 - vector);
			vector17 = vector + frameData.m_rotation * new Vector3(0f, 0f, this.m_info.m_generatedInfo.m_wheelBase * 0.5f);
		}
		else
		{
			frameData.m_rotation = Quaternion.LookRotation(vector - vector2);
			vector17 = vector - frameData.m_rotation * new Vector3(0f, 0f, this.m_info.m_generatedInfo.m_wheelBase * 0.5f);
		}
		frameData.m_velocity = vector17 - frameData.m_position;
		if (num != 0)
		{
			frameData.m_position += frameData.m_velocity * 0.6f;
		}
		else
		{
			frameData.m_position += frameData.m_velocity * 0.5f;
		}
		frameData.m_swayVelocity = frameData.m_swayVelocity * (1f - this.m_info.m_dampers) - vector14 * (1f - this.m_info.m_springs) - frameData.m_swayPosition * this.m_info.m_springs;
		frameData.m_swayPosition += frameData.m_swayVelocity * 0.5f;
		frameData.m_steerAngle = 0f;
		frameData.m_travelDistance += vector7.z;
		frameData.m_lightIntensity.x = ((!flag) ? 5f : 0f);
		frameData.m_lightIntensity.y = ((!flag) ? 0f : 5f);
		frameData.m_lightIntensity.z = 0f;
		frameData.m_lightIntensity.w = 0f;
		frameData.m_underground = (vehicleData.m_flags & Vehicle.Flags.Underground) != (Vehicle.Flags)0;
		frameData.m_transition = (vehicleData.m_flags & Vehicle.Flags.Transition) != (Vehicle.Flags)0;
		base.SimulationStep(vehicleID, ref vehicleData, ref frameData, leaderID, ref leaderData, lodPhysics);
	}

	// Token: 0x06004DBB RID: 19899 RVA: 0x0035CA38 File Offset: 0x0035AE38
	public override void FrameDataUpdated(ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData)
	{
		Vector3 vector = frameData.m_position + frameData.m_velocity * 0.5f;
		Vector3 vector2 = frameData.m_rotation * new Vector3(0f, 0f, Mathf.Max(0.5f, this.m_info.m_generatedInfo.m_size.z * 0.5f - 1f));
		vehicleData.m_segment.a = vector - vector2;
		vehicleData.m_segment.b = vector + vector2;
	}

	// Token: 0x06004DBC RID: 19900 RVA: 0x0035CACC File Offset: 0x0035AECC
	private void ForceTrafficLights(ushort vehicleID, ref Vehicle vehicleData, bool reserveSpace)
	{
		uint num = vehicleData.m_path;
		if (num != 0U)
		{
			NetManager instance = Singleton<NetManager>.instance;
			PathManager instance2 = Singleton<PathManager>.instance;
			byte b = vehicleData.m_pathPositionIndex;
			if (b == 255)
			{
				b = 0;
			}
			b = (byte)(b >> 1);
			for (int i = 0; i < 6; i++)
			{
				PathUnit.Position position;
				if (!instance2.m_pathUnits.m_buffer[(int)((UIntPtr)num)].GetPosition((int)b, out position))
				{
					return;
				}
				if (reserveSpace && i >= 1 && i <= 2)
				{
					uint laneID = PathManager.GetLaneID(position);
					if (laneID != 0U)
					{
						reserveSpace = instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].ReserveSpace(this.m_info.m_generatedInfo.m_size.z, vehicleID);
					}
				}
				TrainAI.ForceTrafficLights(position);
				if ((b += 1) >= instance2.m_pathUnits.m_buffer[(int)((UIntPtr)num)].m_positionCount)
				{
					num = instance2.m_pathUnits.m_buffer[(int)((UIntPtr)num)].m_nextPathUnit;
					b = 0;
					if (num == 0U)
					{
						return;
					}
				}
			}
		}
	}

	// Token: 0x06004DBD RID: 19901 RVA: 0x0035CBE8 File Offset: 0x0035AFE8
	private static void ForceTrafficLights(PathUnit.Position position)
	{
		NetManager instance = Singleton<NetManager>.instance;
		ushort num;
		if (position.m_offset < 128)
		{
			num = instance.m_segments.m_buffer[(int)position.m_segment].m_startNode;
		}
		else
		{
			num = instance.m_segments.m_buffer[(int)position.m_segment].m_endNode;
		}
		NetNode.Flags flags = instance.m_nodes.m_buffer[(int)num].m_flags;
		if ((flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None)
		{
			uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
			uint num2 = (uint)(((int)num << 8) / 32768);
			uint num3 = (currentFrameIndex - num2) & 255U;
			RoadBaseAI.TrafficLightState trafficLightState;
			RoadBaseAI.TrafficLightState trafficLightState2;
			bool flag;
			bool flag2;
			RoadBaseAI.GetTrafficLightState(num, ref instance.m_segments.m_buffer[(int)position.m_segment], currentFrameIndex - num2, out trafficLightState, out trafficLightState2, out flag, out flag2);
			if (!flag && num3 >= 196U)
			{
				flag = true;
				RoadBaseAI.SetTrafficLightState(num, ref instance.m_segments.m_buffer[(int)position.m_segment], currentFrameIndex - num2, trafficLightState, trafficLightState2, flag, flag2);
			}
		}
	}

	// Token: 0x06004DBE RID: 19902 RVA: 0x0035CCF8 File Offset: 0x0035B0F8
	protected void UpdatePathTargetPositions(ushort vehicleID, ref Vehicle vehicleData, Vector3 refPos1, Vector3 refPos2, ushort leaderID, ref Vehicle leaderData, ref int index, int max1, int max2, float minSqrDistanceA, float minSqrDistanceB)
	{
		PathManager instance = Singleton<PathManager>.instance;
		NetManager instance2 = Singleton<NetManager>.instance;
		Vector4 vector = vehicleData.m_targetPos0;
		vector.w = 1000f;
		float num = minSqrDistanceA;
		float num2 = 0f;
		uint num3 = vehicleData.m_path;
		byte b = vehicleData.m_pathPositionIndex;
		byte b2 = vehicleData.m_lastPathOffset;
		if (b == 255)
		{
			b = 0;
			if (index <= 0)
			{
				vehicleData.m_pathPositionIndex = 0;
			}
			if (!Singleton<PathManager>.instance.m_pathUnits.m_buffer[(int)((UIntPtr)num3)].CalculatePathPositionOffset(b >> 1, vector, out b2))
			{
				this.InvalidPath(vehicleID, ref vehicleData, leaderID, ref leaderData);
				return;
			}
		}
		PathUnit.Position position;
		if (!instance.m_pathUnits.m_buffer[(int)((UIntPtr)num3)].GetPosition(b >> 1, out position))
		{
			this.InvalidPath(vehicleID, ref vehicleData, leaderID, ref leaderData);
			return;
		}
		uint num4 = PathManager.GetLaneID(position);
		Bezier3 bezier;
		for (;;)
		{
			if ((b & 1) == 0)
			{
				bool flag = true;
				while (b2 != position.m_offset)
				{
					if (flag)
					{
						flag = false;
					}
					else
					{
						float num5 = Mathf.Max(Mathf.Sqrt(num) - Vector3.Distance(vector, refPos1), Mathf.Sqrt(num2) - Vector3.Distance(vector, refPos2));
						int num6;
						if (num5 < 0f)
						{
							num6 = 4;
						}
						else
						{
							num6 = 4 + Mathf.CeilToInt(num5 * 256f / (instance2.m_lanes.m_buffer[(int)((UIntPtr)num4)].m_length + 1f));
						}
						if (b2 > position.m_offset)
						{
							b2 = (byte)Mathf.Max((int)b2 - num6, (int)position.m_offset);
						}
						else if (b2 < position.m_offset)
						{
							b2 = (byte)Mathf.Min((int)b2 + num6, (int)position.m_offset);
						}
					}
					Vector3 vector2;
					Vector3 vector3;
					float num7;
					this.CalculateSegmentPosition(vehicleID, ref vehicleData, position, num4, b2, out vector2, out vector3, out num7);
					vector.Set(vector2.x, vector2.y, vector2.z, Mathf.Min(vector.w, num7));
					float sqrMagnitude = (vector2 - refPos1).sqrMagnitude;
					float sqrMagnitude2 = (vector2 - refPos2).sqrMagnitude;
					if (sqrMagnitude >= num && sqrMagnitude2 >= num2)
					{
						if (index <= 0)
						{
							vehicleData.m_lastPathOffset = b2;
						}
						vehicleData.SetTargetPos(index++, vector);
						if (index < max1)
						{
							num = minSqrDistanceB;
							refPos1 = vector;
						}
						else if (index == max1)
						{
							num = (refPos2 - refPos1).sqrMagnitude;
							num2 = minSqrDistanceA;
						}
						else
						{
							num2 = minSqrDistanceB;
							refPos2 = vector;
						}
						vector.w = 1000f;
						if (index == max2)
						{
							return;
						}
					}
				}
				b += 1;
				b2 = 0;
				if (index <= 0)
				{
					vehicleData.m_pathPositionIndex = b;
					vehicleData.m_lastPathOffset = b2;
				}
			}
			int num8 = (b >> 1) + 1;
			uint num9 = num3;
			if (num8 >= (int)instance.m_pathUnits.m_buffer[(int)((UIntPtr)num3)].m_positionCount)
			{
				num8 = 0;
				num9 = instance.m_pathUnits.m_buffer[(int)((UIntPtr)num3)].m_nextPathUnit;
				if (num9 == 0U)
				{
					goto Block_19;
				}
			}
			PathUnit.Position position2;
			if (!instance.m_pathUnits.m_buffer[(int)((UIntPtr)num9)].GetPosition(num8, out position2))
			{
				goto Block_21;
			}
			NetInfo info = instance2.m_segments.m_buffer[(int)position2.m_segment].Info;
			if (info.m_lanes.Length <= (int)position2.m_lane)
			{
				goto Block_22;
			}
			uint laneID = PathManager.GetLaneID(position2);
			NetInfo.Lane lane = info.m_lanes[(int)position2.m_lane];
			if (lane.m_laneType != NetInfo.LaneType.Vehicle)
			{
				goto Block_23;
			}
			if (position2.m_segment != position.m_segment && leaderID != 0)
			{
				leaderData.m_flags &= ~Vehicle.Flags.Leaving;
			}
			byte b3 = 0;
			if (num4 != laneID)
			{
				PathUnit.CalculatePathPositionOffset(laneID, vector, out b3);
				bezier = default(Bezier3);
				Vector3 vector4;
				float num10;
				this.CalculateSegmentPosition(vehicleID, ref vehicleData, position, num4, position.m_offset, out bezier.a, out vector4, out num10);
				bool flag2;
				if ((leaderData.m_flags & Vehicle.Flags.Reversed) != (Vehicle.Flags)0)
				{
					flag2 = vehicleData.m_trailingVehicle == 0;
				}
				else
				{
					flag2 = vehicleData.m_leadingVehicle == 0;
				}
				bool flag3 = flag2 && b2 == 0;
				Vector3 vector5;
				float num11;
				this.CalculateSegmentPosition(vehicleID, ref vehicleData, position2, laneID, b3, out bezier.d, out vector5, out num11);
				if (position.m_offset == 0)
				{
					vector4 = -vector4;
				}
				if (b3 < position2.m_offset)
				{
					vector5 = -vector5;
				}
				vector4.Normalize();
				vector5.Normalize();
				float num12;
				NetSegment.CalculateMiddlePoints(bezier.a, vector4, bezier.d, vector5, true, true, out bezier.b, out bezier.c, out num12);
				if (flag3)
				{
					this.CheckNextLane(vehicleID, ref vehicleData, ref num11, position2, laneID, b3, position, num4, position.m_offset, bezier);
				}
				if (flag2 && (num11 < 0.01f || (instance2.m_segments.m_buffer[(int)position2.m_segment].m_flags & (NetSegment.Flags.Collapsed | NetSegment.Flags.Flooded)) != NetSegment.Flags.None))
				{
					goto IL_0595;
				}
				if (num12 > 1f)
				{
					ushort num13;
					if (b3 == 0)
					{
						num13 = instance2.m_segments.m_buffer[(int)position2.m_segment].m_startNode;
					}
					else if (b3 == 255)
					{
						num13 = instance2.m_segments.m_buffer[(int)position2.m_segment].m_endNode;
					}
					else
					{
						num13 = 0;
					}
					float num14 = 1.5707964f * (1f + Vector3.Dot(vector4, vector5));
					if (num12 > 1f)
					{
						num14 /= num12;
					}
					num11 = Mathf.Min(num11, this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1000f, num14));
					while (b2 < 255)
					{
						float num15 = Mathf.Max(Mathf.Sqrt(num) - Vector3.Distance(vector, refPos1), Mathf.Sqrt(num2) - Vector3.Distance(vector, refPos2));
						int num16;
						if (num15 < 0f)
						{
							num16 = 8;
						}
						else
						{
							num16 = 8 + Mathf.CeilToInt(num15 * 256f / (num12 + 1f));
						}
						b2 = (byte)Mathf.Min((int)b2 + num16, 255);
						Vector3 vector6 = bezier.Position((float)b2 * 0.003921569f);
						vector.Set(vector6.x, vector6.y, vector6.z, Mathf.Min(vector.w, num11));
						float sqrMagnitude3 = (vector6 - refPos1).sqrMagnitude;
						float sqrMagnitude4 = (vector6 - refPos2).sqrMagnitude;
						if (sqrMagnitude3 >= num && sqrMagnitude4 >= num2)
						{
							if (index <= 0)
							{
								vehicleData.m_lastPathOffset = b2;
							}
							if (num13 != 0)
							{
								this.UpdateNodeTargetPos(vehicleID, ref vehicleData, num13, ref instance2.m_nodes.m_buffer[(int)num13], ref vector, index);
							}
							vehicleData.SetTargetPos(index++, vector);
							if (index < max1)
							{
								num = minSqrDistanceB;
								refPos1 = vector;
							}
							else if (index == max1)
							{
								num = (refPos2 - refPos1).sqrMagnitude;
								num2 = minSqrDistanceA;
							}
							else
							{
								num2 = minSqrDistanceB;
								refPos2 = vector;
							}
							vector.w = 1000f;
							if (index == max2)
							{
								return;
							}
						}
					}
				}
			}
			else
			{
				PathUnit.CalculatePathPositionOffset(laneID, vector, out b3);
			}
			if (index <= 0)
			{
				if (num8 == 0)
				{
					Singleton<PathManager>.instance.ReleaseFirstUnit(ref vehicleData.m_path);
				}
				if (num8 >= (int)(instance.m_pathUnits.m_buffer[(int)((UIntPtr)num9)].m_positionCount - 1) && instance.m_pathUnits.m_buffer[(int)((UIntPtr)num9)].m_nextPathUnit == 0U && leaderID != 0)
				{
					this.ArrivingToDestination(leaderID, ref leaderData);
				}
			}
			num3 = num9;
			b = (byte)(num8 << 1);
			b2 = b3;
			if (index <= 0)
			{
				vehicleData.m_pathPositionIndex = b;
				vehicleData.m_lastPathOffset = b2;
				vehicleData.m_flags = (vehicleData.m_flags & ~(Vehicle.Flags.OnGravel | Vehicle.Flags.Underground | Vehicle.Flags.Transition)) | info.m_setVehicleFlags;
				if ((vehicleData.m_flags2 & Vehicle.Flags2.Yielding) != (Vehicle.Flags2)0)
				{
					vehicleData.m_flags2 &= ~Vehicle.Flags2.Yielding;
					vehicleData.m_waitCounter = 0;
				}
			}
			position = position2;
			num4 = laneID;
		}
		return;
		Block_19:
		if (index <= 0)
		{
			Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
			vehicleData.m_path = 0U;
		}
		vector.w = 1f;
		vehicleData.SetTargetPos(index++, vector);
		return;
		Block_21:
		this.InvalidPath(vehicleID, ref vehicleData, leaderID, ref leaderData);
		return;
		Block_22:
		this.InvalidPath(vehicleID, ref vehicleData, leaderID, ref leaderData);
		return;
		Block_23:
		this.InvalidPath(vehicleID, ref vehicleData, leaderID, ref leaderData);
		return;
		IL_0595:
		if (index <= 0)
		{
			vehicleData.m_lastPathOffset = b2;
		}
		vector = bezier.a;
		vector.w = 0f;
		while (index < max2)
		{
			vehicleData.SetTargetPos(index++, vector);
		}
	}

	// Token: 0x06004DBF RID: 19903 RVA: 0x0035D62C File Offset: 0x0035BA2C
	private static bool CheckOverlap(ushort vehicleID, ref Vehicle vehicleData, Segment3 segment, ushort ignoreVehicle)
	{
		VehicleManager instance = Singleton<VehicleManager>.instance;
		Vector3 vector = segment.Min();
		Vector3 vector2 = segment.Max();
		int num = Mathf.Max((int)((vector.x - 30f) / 32f + 270f), 0);
		int num2 = Mathf.Max((int)((vector.z - 30f) / 32f + 270f), 0);
		int num3 = Mathf.Min((int)((vector2.x + 30f) / 32f + 270f), 539);
		int num4 = Mathf.Min((int)((vector2.z + 30f) / 32f + 270f), 539);
		bool flag = false;
		for (int i = num2; i <= num4; i++)
		{
			for (int j = num; j <= num3; j++)
			{
				ushort num5 = instance.m_vehicleGrid[i * 540 + j];
				int num6 = 0;
				while (num5 != 0)
				{
					num5 = TrainAI.CheckOverlap(vehicleID, ref vehicleData, segment, ignoreVehicle, num5, ref instance.m_vehicles.m_buffer[(int)num5], ref flag, vector, vector2);
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

	// Token: 0x06004DC0 RID: 19904 RVA: 0x0035D784 File Offset: 0x0035BB84
	private static ushort CheckOverlap(ushort vehicleID, ref Vehicle vehicleData, Segment3 segment, ushort ignoreVehicle, ushort otherID, ref Vehicle otherData, ref bool overlap, Vector3 min, Vector3 max)
	{
		if (ignoreVehicle == 0 || (otherID != ignoreVehicle && otherData.m_leadingVehicle != ignoreVehicle && otherData.m_trailingVehicle != ignoreVehicle))
		{
			VehicleInfo info = otherData.Info;
			if (info.m_vehicleType == VehicleInfo.VehicleType.Bicycle)
			{
				return otherData.m_nextGridVehicle;
			}
			if (((vehicleData.m_flags | otherData.m_flags) & Vehicle.Flags.Transition) == (Vehicle.Flags)0 && (vehicleData.m_flags & Vehicle.Flags.Underground) != (otherData.m_flags & Vehicle.Flags.Underground))
			{
				return otherData.m_nextGridVehicle;
			}
			Vector3 vector = Vector3.Min(otherData.m_segment.Min(), otherData.m_targetPos3);
			Vector3 vector2 = Vector3.Max(otherData.m_segment.Max(), otherData.m_targetPos3);
			if (min.x < vector2.x + 2f && min.y < vector2.y + 2f && min.z < vector2.z + 2f && vector.x < max.x + 2f && vector.y < max.y + 2f && vector.z < max.z + 2f)
			{
				Vector3 vector3 = Vector3.Normalize(segment.b - segment.a);
				Vector3 vector4 = otherData.m_segment.a - vehicleData.m_segment.b;
				Vector3 vector5 = otherData.m_segment.b - vehicleData.m_segment.b;
				if (Vector3.Dot(vector4, vector3) >= 1f || Vector3.Dot(vector5, vector3) >= 1f)
				{
					float num2;
					float num3;
					float num = segment.DistanceSqr(otherData.m_segment, ref num2, ref num3);
					if (num < 4f)
					{
						overlap = true;
					}
					Vector3 vector6 = otherData.m_segment.b;
					segment.a.y = segment.a.y * 0.5f;
					segment.b.y = segment.b.y * 0.5f;
					for (int i = 0; i < 4; i++)
					{
						Vector3 vector7 = otherData.GetTargetPos(i);
						Segment3 segment2;
						segment2..ctor(vector6, vector7);
						segment2.a.y = segment2.a.y * 0.5f;
						segment2.b.y = segment2.b.y * 0.5f;
						if (segment2.LengthSqr() > 0.01f)
						{
							num = segment.DistanceSqr(segment2, ref num2, ref num3);
							if (num < 4f)
							{
								overlap = true;
								break;
							}
						}
						vector6 = vector7;
					}
				}
			}
		}
		return otherData.m_nextGridVehicle;
	}

	// Token: 0x06004DC1 RID: 19905 RVA: 0x0035DA64 File Offset: 0x0035BE64
	protected override float CalculateTargetSpeed(ushort vehicleID, ref Vehicle data, float speedLimit, float curve)
	{
		float num = Mathf.Min(this.m_info.m_maxSpeed, 8f * speedLimit);
		if (this.m_info.m_class.m_subService == ItemClass.SubService.PublicTransportTrain)
		{
			int factor = Singleton<BuildingManager>.instance.m_finalMonumentEffect[6].m_factor;
			if (factor != 0)
			{
				num += num * (float)factor * 0.01f;
			}
		}
		float num2 = 1000f / (1f + curve * 2000f / this.m_info.m_turning) + this.m_minTurningSpeed;
		return Mathf.Min(num, num2);
	}

	// Token: 0x06004DC2 RID: 19906 RVA: 0x0035DAF8 File Offset: 0x0035BEF8
	private void CheckNextLane(ushort vehicleID, ref Vehicle vehicleData, ref float maxSpeed, PathUnit.Position position, uint laneID, byte offset, PathUnit.Position prevPos, uint prevLaneID, byte prevOffset, Bezier3 bezier)
	{
		NetManager instance = Singleton<NetManager>.instance;
		Vehicle.Frame lastFrameData = vehicleData.GetLastFrameData();
		Vector3 vector = lastFrameData.m_position;
		Vector3 vector2 = lastFrameData.m_position;
		Vector3 vector3 = lastFrameData.m_rotation * new Vector3(0f, 0f, this.m_info.m_generatedInfo.m_wheelBase * 0.5f);
		vector += vector3;
		vector2 -= vector3;
		float num = 0.5f * lastFrameData.m_velocity.sqrMagnitude / this.m_info.m_braking;
		float num2 = Vector3.Distance(vector, bezier.a);
		float num3 = Vector3.Distance(vector2, bezier.a);
		if (Mathf.Min(num2, num3) >= num - 5f)
		{
			if (!instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CheckSpace(1000f, vehicleID))
			{
				vehicleData.m_flags2 |= Vehicle.Flags2.Yielding;
				vehicleData.m_waitCounter = 0;
				maxSpeed = 0f;
				return;
			}
			Vector3 vector4 = bezier.Position(0.5f);
			Segment3 segment;
			if (Vector3.SqrMagnitude(vehicleData.m_segment.a - vector4) < Vector3.SqrMagnitude(bezier.a - vector4))
			{
				segment..ctor(vehicleData.m_segment.a, vector4);
			}
			else
			{
				segment..ctor(bezier.a, vector4);
			}
			if (segment.LengthSqr() >= 3f)
			{
				segment.a += (segment.b - segment.a).normalized * 2.5f;
				if (TrainAI.CheckOverlap(vehicleID, ref vehicleData, segment, vehicleID))
				{
					vehicleData.m_flags2 |= Vehicle.Flags2.Yielding;
					vehicleData.m_waitCounter = 0;
					maxSpeed = 0f;
					return;
				}
			}
			segment..ctor(vector4, bezier.d);
			if (segment.LengthSqr() >= 1f && TrainAI.CheckOverlap(vehicleID, ref vehicleData, segment, vehicleID))
			{
				vehicleData.m_flags2 |= Vehicle.Flags2.Yielding;
				vehicleData.m_waitCounter = 0;
				maxSpeed = 0f;
				return;
			}
			if (this.m_info.m_vehicleType != VehicleInfo.VehicleType.Monorail)
			{
				ushort num4;
				if (offset < position.m_offset)
				{
					num4 = instance.m_segments.m_buffer[(int)position.m_segment].m_startNode;
				}
				else
				{
					num4 = instance.m_segments.m_buffer[(int)position.m_segment].m_endNode;
				}
				ushort num5;
				if (prevOffset == 0)
				{
					num5 = instance.m_segments.m_buffer[(int)prevPos.m_segment].m_startNode;
				}
				else
				{
					num5 = instance.m_segments.m_buffer[(int)prevPos.m_segment].m_endNode;
				}
				if (num4 == num5)
				{
					NetNode.Flags flags = instance.m_nodes.m_buffer[(int)num4].m_flags;
					NetLane.Flags flags2 = (NetLane.Flags)instance.m_lanes.m_buffer[(int)((UIntPtr)prevLaneID)].m_flags;
					bool flag = (flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None;
					bool flag2 = (flags2 & (NetLane.Flags.YieldStart | NetLane.Flags.YieldEnd)) != NetLane.Flags.None && (flags & (NetNode.Flags.Junction | NetNode.Flags.TrafficLights | NetNode.Flags.OneWayIn)) == NetNode.Flags.Junction;
					if (flag)
					{
						uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
						uint num6 = (uint)(((int)num5 << 8) / 32768);
						uint num7 = (currentFrameIndex - num6) & 255U;
						RoadBaseAI.TrafficLightState trafficLightState;
						RoadBaseAI.TrafficLightState trafficLightState2;
						bool flag3;
						bool flag4;
						RoadBaseAI.GetTrafficLightState(num5, ref instance.m_segments.m_buffer[(int)prevPos.m_segment], currentFrameIndex - num6, out trafficLightState, out trafficLightState2, out flag3, out flag4);
						if (!flag3 && num7 >= 196U)
						{
							flag3 = true;
							RoadBaseAI.SetTrafficLightState(num5, ref instance.m_segments.m_buffer[(int)prevPos.m_segment], currentFrameIndex - num6, trafficLightState, trafficLightState2, flag3, flag4);
						}
						if (trafficLightState != RoadBaseAI.TrafficLightState.RedToGreen)
						{
							if (trafficLightState != RoadBaseAI.TrafficLightState.GreenToRed)
							{
								if (trafficLightState == RoadBaseAI.TrafficLightState.Red)
								{
									vehicleData.m_flags2 |= Vehicle.Flags2.Yielding;
									vehicleData.m_waitCounter = 0;
									maxSpeed = 0f;
									return;
								}
							}
							else if (num7 >= 30U)
							{
								vehicleData.m_flags2 |= Vehicle.Flags2.Yielding;
								vehicleData.m_waitCounter = 0;
								maxSpeed = 0f;
								return;
							}
						}
						else if (num7 < 60U)
						{
							vehicleData.m_flags2 |= Vehicle.Flags2.Yielding;
							vehicleData.m_waitCounter = 0;
							maxSpeed = 0f;
							return;
						}
					}
					if (flag2 && (vehicleData.m_flags2 & Vehicle.Flags2.Yielding) != (Vehicle.Flags2)0)
					{
						vehicleData.m_waitCounter = (byte)Mathf.Min((int)(vehicleData.m_waitCounter + 1), 4);
						if (vehicleData.m_waitCounter < 4)
						{
							maxSpeed = 0f;
							return;
						}
						vehicleData.m_flags2 &= ~Vehicle.Flags2.Yielding;
						vehicleData.m_waitCounter = 0;
					}
				}
			}
		}
	}

	// Token: 0x06004DC3 RID: 19907 RVA: 0x0035DFB8 File Offset: 0x0035C3B8
	protected override void CalculateSegmentPosition(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position position, uint laneID, byte offset, out Vector3 pos, out Vector3 dir, out float maxSpeed)
	{
		NetManager instance = Singleton<NetManager>.instance;
		instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CalculatePositionAndDirection((float)offset * 0.003921569f, out pos, out dir);
		NetInfo info = instance.m_segments.m_buffer[(int)position.m_segment].Info;
		if (info.m_lanes != null && info.m_lanes.Length > (int)position.m_lane)
		{
			maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, info.m_lanes[(int)position.m_lane].m_speedLimit, instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].m_curve);
		}
		else
		{
			maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1f, 0f);
		}
	}

	// Token: 0x06004DC4 RID: 19908 RVA: 0x0035E07C File Offset: 0x0035C47C
	private static float GetMaxSpeed(ushort leaderID, ref Vehicle leaderData)
	{
		float num = 1000000f;
		VehicleManager instance = Singleton<VehicleManager>.instance;
		ushort num2 = leaderID;
		int num3 = 0;
		while (num2 != 0)
		{
			num = Mathf.Min(num, instance.m_vehicles.m_buffer[(int)num2].m_targetPos0.w);
			num = Mathf.Min(num, instance.m_vehicles.m_buffer[(int)num2].m_targetPos1.w);
			num2 = instance.m_vehicles.m_buffer[(int)num2].m_trailingVehicle;
			if (++num3 > 16384)
			{
				CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
				break;
			}
		}
		return num;
	}

	// Token: 0x06004DC5 RID: 19909 RVA: 0x0035E12C File Offset: 0x0035C52C
	private static void Reverse(ushort leaderID, ref Vehicle leaderData)
	{
		if ((leaderData.m_flags & Vehicle.Flags.Reversed) != (Vehicle.Flags)0)
		{
			leaderData.m_flags &= ~Vehicle.Flags.Reversed;
		}
		else
		{
			leaderData.m_flags |= Vehicle.Flags.Reversed;
		}
		VehicleManager instance = Singleton<VehicleManager>.instance;
		ushort num = leaderID;
		int num2 = 0;
		while (num != 0)
		{
			TrainAI.ResetTargets(num, ref instance.m_vehicles.m_buffer[(int)num], leaderID, ref leaderData, true);
			instance.m_vehicles.m_buffer[(int)num].m_flags = (instance.m_vehicles.m_buffer[(int)num].m_flags & ~Vehicle.Flags.Reversed) | (leaderData.m_flags & Vehicle.Flags.Reversed);
			num = instance.m_vehicles.m_buffer[(int)num].m_trailingVehicle;
			if (++num2 > 16384)
			{
				CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
				break;
			}
		}
	}

	// Token: 0x06004DC6 RID: 19910 RVA: 0x0035E224 File Offset: 0x0035C624
	private static void ResetTargets(ushort vehicleID, ref Vehicle vehicleData, ushort leaderID, ref Vehicle leaderData, bool pushPathPos)
	{
		Vehicle.Frame lastFrameData = vehicleData.GetLastFrameData();
		VehicleInfo info = vehicleData.Info;
		TrainAI trainAI = info.m_vehicleAI as TrainAI;
		Vector3 vector = lastFrameData.m_position;
		Vector3 vector2 = lastFrameData.m_position;
		Vector3 vector3 = lastFrameData.m_rotation * new Vector3(0f, 0f, info.m_generatedInfo.m_wheelBase * 0.5f);
		if ((leaderData.m_flags & Vehicle.Flags.Reversed) != (Vehicle.Flags)0)
		{
			vector -= vector3;
			vector2 += vector3;
		}
		else
		{
			vector += vector3;
			vector2 -= vector3;
		}
		vehicleData.m_targetPos0 = vector2;
		vehicleData.m_targetPos0.w = 2f;
		vehicleData.m_targetPos1 = vector;
		vehicleData.m_targetPos1.w = 2f;
		vehicleData.m_targetPos2 = vehicleData.m_targetPos1;
		vehicleData.m_targetPos3 = vehicleData.m_targetPos1;
		if (vehicleData.m_path != 0U)
		{
			PathManager instance = Singleton<PathManager>.instance;
			int num = (vehicleData.m_pathPositionIndex >> 1) + 1;
			uint num2 = vehicleData.m_path;
			if (num >= (int)instance.m_pathUnits.m_buffer[(int)((UIntPtr)num2)].m_positionCount)
			{
				num = 0;
				num2 = instance.m_pathUnits.m_buffer[(int)((UIntPtr)num2)].m_nextPathUnit;
			}
			PathUnit.Position position;
			if (instance.m_pathUnits.m_buffer[(int)((UIntPtr)vehicleData.m_path)].GetPosition(vehicleData.m_pathPositionIndex >> 1, out position))
			{
				uint laneID = PathManager.GetLaneID(position);
				PathUnit.Position position2;
				if (num2 != 0U && instance.m_pathUnits.m_buffer[(int)((UIntPtr)num2)].GetPosition(num, out position2))
				{
					uint laneID2 = PathManager.GetLaneID(position2);
					if (laneID2 == laneID)
					{
						if (num2 != vehicleData.m_path)
						{
							instance.ReleaseFirstUnit(ref vehicleData.m_path);
						}
						vehicleData.m_pathPositionIndex = (byte)(num << 1);
					}
				}
				PathUnit.CalculatePathPositionOffset(laneID, vector2, out vehicleData.m_lastPathOffset);
			}
		}
		if (vehicleData.m_path != 0U)
		{
			int num3 = 0;
			trainAI.UpdatePathTargetPositions(vehicleID, ref vehicleData, vector2, vector, 0, ref leaderData, ref num3, 1, 4, 4f, 1f);
		}
	}

	// Token: 0x06004DC7 RID: 19911 RVA: 0x0035E44C File Offset: 0x0035C84C
	private static void InitializePath(ushort vehicleID, ref Vehicle vehicleData)
	{
		PathManager instance = Singleton<PathManager>.instance;
		VehicleManager instance2 = Singleton<VehicleManager>.instance;
		ushort num = vehicleData.m_trailingVehicle;
		int num2 = 0;
		while (num != 0)
		{
			if (instance2.m_vehicles.m_buffer[(int)num].m_path != 0U)
			{
				instance.ReleasePath(instance2.m_vehicles.m_buffer[(int)num].m_path);
				instance2.m_vehicles.m_buffer[(int)num].m_path = 0U;
			}
			if (instance.AddPathReference(vehicleData.m_path))
			{
				instance2.m_vehicles.m_buffer[(int)num].m_path = vehicleData.m_path;
				instance2.m_vehicles.m_buffer[(int)num].m_pathPositionIndex = 0;
			}
			TrainAI.ResetTargets(num, ref instance2.m_vehicles.m_buffer[(int)num], vehicleID, ref vehicleData, false);
			num = instance2.m_vehicles.m_buffer[(int)num].m_trailingVehicle;
			if (++num2 > 16384)
			{
				CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
				break;
			}
		}
		vehicleData.m_pathPositionIndex = 0;
		TrainAI.ResetTargets(vehicleID, ref vehicleData, vehicleID, ref vehicleData, false);
	}

	// Token: 0x06004DC8 RID: 19912 RVA: 0x0035E578 File Offset: 0x0035C978
	private static float CalculateMaxSpeed(float targetDistance, float targetSpeed, float maxBraking)
	{
		float num = 0.5f * maxBraking;
		float num2 = num + targetSpeed;
		return Mathf.Sqrt(Mathf.Max(0f, num2 * num2 + 2f * targetDistance * maxBraking)) - num;
	}

	// Token: 0x06004DC9 RID: 19913 RVA: 0x0035E5AF File Offset: 0x0035C9AF
	protected override bool StartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos)
	{
		return this.StartPathFind(vehicleID, ref vehicleData, startPos, endPos, true, true);
	}

	// Token: 0x06004DCA RID: 19914 RVA: 0x0035E5C0 File Offset: 0x0035C9C0
	protected bool StartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays)
	{
		VehicleInfo info = this.m_info;
		if ((vehicleData.m_flags & Vehicle.Flags.Spawned) == (Vehicle.Flags)0 && Vector3.Distance(startPos, endPos) < 100f)
		{
			startPos = endPos;
		}
		bool flag;
		bool flag2;
		if (info.m_vehicleType == VehicleInfo.VehicleType.Metro)
		{
			flag = true;
			flag2 = true;
		}
		else
		{
			flag = (vehicleData.m_flags & (Vehicle.Flags.Underground | Vehicle.Flags.Transition)) != (Vehicle.Flags)0;
			flag2 = false;
		}
		PathUnit.Position position;
		PathUnit.Position position2;
		float num;
		float num2;
		PathUnit.Position position3;
		PathUnit.Position position4;
		float num3;
		float num4;
		if (PathManager.FindPathPosition(startPos, this.m_transportInfo.m_netService, this.m_transportInfo.m_secondaryNetService, NetInfo.LaneType.Vehicle, info.m_vehicleType, info.vehicleCategory, VehicleInfo.VehicleType.None, flag, false, 32f, false, false, out position, out position2, out num, out num2) && PathManager.FindPathPosition(endPos, this.m_transportInfo.m_netService, this.m_transportInfo.m_secondaryNetService, NetInfo.LaneType.Vehicle, info.m_vehicleType, info.vehicleCategory, VehicleInfo.VehicleType.None, flag2, false, 32f, false, false, out position3, out position4, out num3, out num4))
		{
			if (!startBothWays || num2 > num * 1.2f)
			{
				position2 = default(PathUnit.Position);
			}
			if (!endBothWays || num4 > num3 * 1.2f)
			{
				position4 = default(PathUnit.Position);
			}
			uint num5;
			if (Singleton<PathManager>.instance.CreatePath(out num5, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, position, position2, position3, position4, NetInfo.LaneType.Vehicle, info.m_vehicleType, info.vehicleCategory, 20000f, false, false, true, false))
			{
				if (vehicleData.m_path != 0U)
				{
					Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
				}
				vehicleData.m_path = num5;
				vehicleData.m_flags |= Vehicle.Flags.WaitingPath;
				return true;
			}
		}
		return false;
	}

	// Token: 0x06004DCB RID: 19915 RVA: 0x0035E75C File Offset: 0x0035CB5C
	public override bool TrySpawn(ushort vehicleID, ref Vehicle vehicleData)
	{
		if ((vehicleData.m_flags & Vehicle.Flags.Spawned) != (Vehicle.Flags)0)
		{
			return true;
		}
		if (vehicleData.m_path != 0U)
		{
			PathManager instance = Singleton<PathManager>.instance;
			PathUnit.Position position;
			if (instance.m_pathUnits.m_buffer[(int)((UIntPtr)vehicleData.m_path)].GetPosition(0, out position))
			{
				uint laneID = PathManager.GetLaneID(position);
				if (laneID != 0U && !Singleton<NetManager>.instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CheckSpace(1000f, vehicleID))
				{
					vehicleData.m_flags |= Vehicle.Flags.WaitingSpace;
					return false;
				}
			}
		}
		vehicleData.Spawn(vehicleID);
		vehicleData.m_flags &= ~Vehicle.Flags.WaitingSpace;
		TrainAI.InitializePath(vehicleID, ref vehicleData);
		return true;
	}

	// Token: 0x06004DCC RID: 19916 RVA: 0x0035E818 File Offset: 0x0035CC18
	protected virtual bool PathFindReady(ushort vehicleID, ref Vehicle vehicleData)
	{
		PathManager instance = Singleton<PathManager>.instance;
		NetManager instance2 = Singleton<NetManager>.instance;
		float num = vehicleData.CalculateTotalLength(vehicleID);
		float num2 = (num + this.m_info.m_generatedInfo.m_wheelBase - this.m_info.m_generatedInfo.m_size.z) * 0.5f;
		Vector3 vector = vehicleData.GetLastFramePosition();
		PathUnit.Position position;
		if ((vehicleData.m_flags & Vehicle.Flags.Spawned) == (Vehicle.Flags)0 && instance.m_pathUnits.m_buffer[(int)((UIntPtr)vehicleData.m_path)].GetPosition(0, out position))
		{
			uint laneID = PathManager.GetLaneID(position);
			vector = instance2.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CalculatePosition((float)position.m_offset * 0.003921569f);
		}
		vehicleData.m_flags &= ~Vehicle.Flags.WaitingPath;
		instance.m_pathUnits.m_buffer[(int)((UIntPtr)vehicleData.m_path)].MoveLastPosition(vehicleData.m_path, num2);
		if ((vehicleData.m_flags & Vehicle.Flags.Spawned) != (Vehicle.Flags)0)
		{
			TrainAI.InitializePath(vehicleID, ref vehicleData);
		}
		else
		{
			int num3 = Mathf.Min(1, (int)(instance.m_pathUnits.m_buffer[(int)((UIntPtr)vehicleData.m_path)].m_positionCount - 1));
			PathUnit.Position position2;
			if (instance.m_pathUnits.m_buffer[(int)((UIntPtr)vehicleData.m_path)].GetPosition(num3, out position2))
			{
				uint laneID2 = PathManager.GetLaneID(position2);
				Vector3 vector2 = instance2.m_lanes.m_buffer[(int)((UIntPtr)laneID2)].CalculatePosition((float)position2.m_offset * 0.003921569f);
				Vector3 vector3 = vector2 - vector;
				vehicleData.m_frame0.m_position = vector;
				if (vector3.sqrMagnitude > 1f)
				{
					float length = instance2.m_lanes.m_buffer[(int)((UIntPtr)laneID2)].m_length;
					vehicleData.m_frame0.m_position = vehicleData.m_frame0.m_position + vector3.normalized * Mathf.Min(length * 0.5f, (num - this.m_info.m_generatedInfo.m_size.z) * 0.5f);
					vehicleData.m_frame0.m_rotation = Quaternion.LookRotation(vector3);
				}
				vehicleData.m_frame1 = vehicleData.m_frame0;
				vehicleData.m_frame2 = vehicleData.m_frame0;
				vehicleData.m_frame3 = vehicleData.m_frame0;
				this.FrameDataUpdated(vehicleID, ref vehicleData, ref vehicleData.m_frame0);
			}
			this.TrySpawn(vehicleID, ref vehicleData);
		}
		return true;
	}

	// Token: 0x06004DCD RID: 19917 RVA: 0x0035EA7C File Offset: 0x0035CE7C
	public override int GetNoiseLevel()
	{
		ItemClass.SubService subService = this.m_info.m_class.m_subService;
		if (subService == ItemClass.SubService.PublicTransportMonorail)
		{
			return 7;
		}
		if (subService != ItemClass.SubService.PublicTransportMetro)
		{
			return 9;
		}
		return 8;
	}

	// Token: 0x04005086 RID: 20614
	public TransportInfo m_transportInfo;

	// Token: 0x04005087 RID: 20615
	public float m_minTurningSpeed = 8f;
}
