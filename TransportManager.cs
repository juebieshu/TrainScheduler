using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.IO;
using ColossalFramework.Math;
using ColossalFramework.Threading;
using UnityEngine;

// Token: 0x020005EA RID: 1514
public class TransportManager : SimulationManagerBase<TransportManager, TransportProperties>, ISimulationManager, IRenderableManager
{
	// Token: 0x170004E7 RID: 1255
	// (get) Token: 0x060044DA RID: 17626 RVA: 0x0031F7AE File Offset: 0x0031DBAE
	// (set) Token: 0x060044DB RID: 17627 RVA: 0x0031F7B6 File Offset: 0x0031DBB6
	public int LinesVisible
	{
		get
		{
			return this.m_linesVisible;
		}
		set
		{
			if (this.m_linesVisible != value)
			{
				this.m_linesVisible = value;
				this.UpdateVisibility();
			}
		}
	}

	// Token: 0x170004E8 RID: 1256
	// (get) Token: 0x060044DC RID: 17628 RVA: 0x0031F7D1 File Offset: 0x0031DBD1
	// (set) Token: 0x060044DD RID: 17629 RVA: 0x0031F7D9 File Offset: 0x0031DBD9
	public bool TunnelsVisible
	{
		get
		{
			return this.m_tunnelsVisible;
		}
		set
		{
			if (this.m_tunnelsVisible != value)
			{
				this.m_tunnelsVisible = value;
				this.UpdateVisibility();
			}
		}
	}

	// Token: 0x170004E9 RID: 1257
	// (get) Token: 0x060044DE RID: 17630 RVA: 0x0031F7F4 File Offset: 0x0031DBF4
	// (set) Token: 0x060044DF RID: 17631 RVA: 0x0031F7FC File Offset: 0x0031DBFC
	public bool TunnelsVisibleInfo
	{
		get
		{
			return this.m_tunnelsVisibleInfo;
		}
		set
		{
			if (this.m_tunnelsVisibleInfo != value)
			{
				this.m_tunnelsVisibleInfo = value;
				this.UpdateVisibility();
			}
		}
	}

	// Token: 0x060044E0 RID: 17632 RVA: 0x0031F818 File Offset: 0x0031DC18
	protected override void Awake()
	{
		base.Awake();
		this.m_lines = new Array16<TransportLine>(256U);
		this.m_lineMeshes = new Mesh[256][];
		this.m_lineMeshData = new RenderGroup.MeshData[256][];
		this.m_lineSegments = new TransportManager.LineSegment[256][];
		this.m_lineCurves = new Bezier3[256][];
		this.m_updatedLines = new ulong[4];
		this.m_metroLayer = LayerMask.NameToLayer("MetroTunnels");
		this.m_materialBlock = new MaterialPropertyBlock();
		this.ID_Color = Shader.PropertyToID("_Color");
		this.ID_StartOffset = Shader.PropertyToID("_StartOffset");
		this.m_patches = new TransportPatch[81];
		this.m_passengers = new TransportPassengerData[17];
		this.m_lineNumber = new ushort[17];
		this.m_transportTypeLoaded = new TransportInfo[17];
		this.m_transportLineVehicleSelection = new Dictionary<ushort, string>();
		this.m_transportLineVehicleSelectionIndices = new Dictionary<ushort, int>();
		ushort num;
		this.m_lines.CreateItem(out num);
	}

	// Token: 0x060044E1 RID: 17633 RVA: 0x0031F91C File Offset: 0x0031DD1C
	private void OnDestroy()
	{
		if (this.m_patches != null)
		{
			int num = this.m_patches.Length;
			for (int i = 0; i < num; i++)
			{
				TransportPatch transportPatch = this.m_patches[i];
				int num2 = 0;
				while (transportPatch != null)
				{
					transportPatch.Release();
					transportPatch = transportPatch.m_nextPatch;
					if (++num2 >= 100)
					{
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}
				this.m_patches[i] = null;
			}
		}
	}

	// Token: 0x060044E2 RID: 17634 RVA: 0x0031F9A4 File Offset: 0x0031DDA4
	public override void InitializeProperties(TransportProperties properties)
	{
		base.InitializeProperties(properties);
		GameObject gameObject = GameObject.FindGameObjectWithTag("UndergroundView");
		if (gameObject != null)
		{
			this.m_undergroundCamera = gameObject.GetComponent<Camera>();
		}
		int num = PrefabCollection<TransportInfo>.LoadedCount();
		for (int i = 0; i < num; i++)
		{
			TransportInfo loaded = PrefabCollection<TransportInfo>.GetLoaded((uint)i);
			if (!(loaded == null))
			{
				this.m_transportTypeLoaded[(int)loaded.m_transportType] = loaded;
			}
		}
	}

	// Token: 0x060044E3 RID: 17635 RVA: 0x0031FA1C File Offset: 0x0031DE1C
	public override void DestroyProperties(TransportProperties properties)
	{
		if (this.m_properties == properties)
		{
			this.m_undergroundCamera = null;
			int num = this.m_patches.Length;
			for (int i = 0; i < num; i++)
			{
				TransportPatch transportPatch = this.m_patches[i];
				int num2 = 0;
				while (transportPatch != null)
				{
					transportPatch.Release();
					transportPatch = transportPatch.m_nextPatch;
					if (++num2 >= 100)
					{
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}
				this.m_patches[i] = null;
			}
			for (int j = 0; j < 17; j++)
			{
				this.m_transportTypeLoaded[j] = null;
			}
		}
		base.DestroyProperties(properties);
	}

	// Token: 0x060044E4 RID: 17636 RVA: 0x0031FAD8 File Offset: 0x0031DED8
	private void UpdateVisibility()
	{
		if (this.m_undergroundCamera != null)
		{
			if (this.m_linesVisible != 0 || this.m_tunnelsVisible || this.m_tunnelsVisibleInfo)
			{
				this.m_undergroundCamera.cullingMask |= 1 << this.m_metroLayer;
			}
			else
			{
				this.m_undergroundCamera.cullingMask &= ~(1 << this.m_metroLayer);
			}
		}
	}

	// Token: 0x060044E5 RID: 17637 RVA: 0x0031FB58 File Offset: 0x0031DF58
	protected override void BeginOverlayImpl(RenderManager.CameraInfo cameraInfo)
	{
		if (this.m_linesVisible != 0 && cameraInfo.m_camera != null)
		{
			int num = this.m_patches.Length;
			for (int i = 0; i < num; i++)
			{
				TransportPatch transportPatch = this.m_patches[i];
				int num2 = 0;
				while (transportPatch != null)
				{
					transportPatch.RenderOverlay(cameraInfo, this.m_linesVisible);
					transportPatch = transportPatch.m_nextPatch;
					if (++num2 >= 100)
					{
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}
			}
		}
		if (this.m_linesVisible != 0 && cameraInfo.m_camera != null)
		{
			this.RenderLines(cameraInfo, cameraInfo.m_camera.cullingMask, this.m_linesVisible);
		}
	}

	// Token: 0x060044E6 RID: 17638 RVA: 0x0031FC20 File Offset: 0x0031E020
	protected override void UndergroundOverlayImpl(RenderManager.CameraInfo cameraInfo)
	{
		if (this.m_linesVisible != 0 && this.m_undergroundCamera != null)
		{
			this.RenderLines(cameraInfo, this.m_undergroundCamera.cullingMask, this.m_linesVisible);
		}
	}

	// Token: 0x060044E7 RID: 17639 RVA: 0x0031FC58 File Offset: 0x0031E058
	private void RenderLines(RenderManager.CameraInfo cameraInfo, int layerMask, int typeMask)
	{
		bool flag = false;
		for (int i = 0; i < 256; i++)
		{
			if (this.m_lines.m_buffer[i].m_flags != TransportLine.Flags.None)
			{
				if ((this.m_lines.m_buffer[i].m_flags & (TransportLine.Flags.Hidden | TransportLine.Flags.Highlighted)) != TransportLine.Flags.Hidden)
				{
					if (this.m_lineMeshData[i] != null)
					{
						this.UpdateMesh((ushort)i);
					}
					if ((this.m_lines.m_buffer[i].m_flags & (TransportLine.Flags.Temporary | TransportLine.Flags.Selected | TransportLine.Flags.Highlighted)) != TransportLine.Flags.None)
					{
						flag = true;
					}
					else
					{
						this.m_lines.m_buffer[i].RenderLine(cameraInfo, layerMask, typeMask, (ushort)i);
					}
				}
			}
			else if (this.m_lineMeshes[i] != null)
			{
				int num = this.m_lineMeshes[i].Length;
				for (int j = 0; j < num; j++)
				{
					Object.Destroy(this.m_lineMeshes[i][j]);
				}
				this.m_lineMeshes[i] = null;
			}
		}
		if (flag)
		{
			for (int k = 0; k < 256; k++)
			{
				if (this.m_lines.m_buffer[k].m_flags != TransportLine.Flags.None && (this.m_lines.m_buffer[k].m_flags & (TransportLine.Flags.Hidden | TransportLine.Flags.Highlighted)) != TransportLine.Flags.Hidden && (this.m_lines.m_buffer[k].m_flags & (TransportLine.Flags.Temporary | TransportLine.Flags.Selected | TransportLine.Flags.Highlighted)) != TransportLine.Flags.None)
				{
					this.m_lines.m_buffer[k].RenderLine(cameraInfo, layerMask, typeMask, (ushort)k);
				}
			}
		}
	}

	// Token: 0x060044E8 RID: 17640 RVA: 0x0031FDFC File Offset: 0x0031E1FC
	private void UpdateMesh(ushort lineID)
	{
		while (!Monitor.TryEnter(this.m_lineMeshData, SimulationManager.SYNCHRONIZE_TIMEOUT))
		{
		}
		RenderGroup.MeshData[] array;
		try
		{
			array = this.m_lineMeshData[(int)lineID];
			this.m_lineMeshData[(int)lineID] = null;
		}
		finally
		{
			Monitor.Exit(this.m_lineMeshData);
		}
		if (array != null)
		{
			Mesh[] array2 = this.m_lineMeshes[(int)lineID];
			int num = 0;
			if (array2 != null)
			{
				num = array2.Length;
			}
			if (num != array.Length)
			{
				Mesh[] array3 = new Mesh[array.Length];
				int num2 = Mathf.Min(num, array3.Length);
				for (int i = 0; i < num2; i++)
				{
					array3[i] = array2[i];
				}
				for (int j = num2; j < array3.Length; j++)
				{
					array3[j] = new Mesh();
				}
				for (int k = num2; k < num; k++)
				{
					Object.Destroy(array2[k]);
				}
				array2 = array3;
				this.m_lineMeshes[(int)lineID] = array2;
			}
			for (int l = 0; l < array.Length; l++)
			{
				array2[l].Clear();
				array2[l].vertices = array[l].m_vertices;
				array2[l].normals = array[l].m_normals;
				array2[l].tangents = array[l].m_tangents;
				array2[l].uv = array[l].m_uvs;
				array2[l].uv2 = array[l].m_uvs2;
				array2[l].colors32 = array[l].m_colors;
				array2[l].triangles = array[l].m_triangles;
				array2[l].bounds = array[l].m_bounds;
			}
		}
	}

	// Token: 0x060044E9 RID: 17641 RVA: 0x0031FFAC File Offset: 0x0031E3AC
	public bool CheckLimits()
	{
		return this.m_lineCount < 251;
	}

	// Token: 0x060044EA RID: 17642 RVA: 0x0031FFC4 File Offset: 0x0031E3C4
	public bool CreateLine(out ushort lineID, ref Randomizer randomizer, TransportInfo info, bool newNumber)
	{
		if (this.m_lines.CreateItem(out lineID, ref randomizer))
		{
			this.m_lines.m_buffer[(int)lineID].m_flags = TransportLine.Flags.Created;
			this.m_lines.m_buffer[(int)lineID].Info = info;
			this.m_lines.m_buffer[(int)lineID].m_vehicles = 0;
			this.m_lines.m_buffer[(int)lineID].m_building = 0;
			this.m_lines.m_buffer[(int)lineID].m_budget = 100;
			this.m_lines.m_buffer[(int)lineID].m_totalLength = 0f;
			this.m_lines.m_buffer[(int)lineID].m_averageInterval = 0;
			this.m_lines.m_buffer[(int)lineID].m_ticketPrice = (ushort)info.m_ticketPrice;
			this.m_lines.m_buffer[(int)lineID].m_bounds = default(Bounds);
			this.m_lines.m_buffer[(int)lineID].m_color = default(Color32);
			this.m_lines.m_buffer[(int)lineID].m_passengers = default(TransportPassengerData);
			if (newNumber)
			{
				TransportLine[] buffer = this.m_lines.m_buffer;
				int num = (int)lineID;
				ushort[] lineNumber = this.m_lineNumber;
				TransportInfo.TransportType transportType = info.m_transportType;
				buffer[num].m_lineNumber = (lineNumber[(int)transportType] = lineNumber[(int)transportType] + 1);
			}
			else
			{
				this.m_lines.m_buffer[(int)lineID].m_lineNumber = 0;
			}
			this.m_lineCount = (int)(this.m_lines.ItemCount() - 1U);
			VehicleInfo.VehicleType vehicleType = info.m_vehicleType;
			if (vehicleType != VehicleInfo.VehicleType.Helicopter)
			{
				if (vehicleType != VehicleInfo.VehicleType.Ferry)
				{
					if (vehicleType != VehicleInfo.VehicleType.Monorail)
					{
						if (vehicleType == VehicleInfo.VehicleType.Blimp)
						{
							Color32 color = this.m_properties.m_transportColors[4];
							this.m_lines.m_buffer[(int)lineID].m_color = new Color32(color.r + 70, color.g + 50, color.b + 70, byte.MaxValue);
							TransportLine[] buffer2 = this.m_lines.m_buffer;
							ushort num2 = lineID;
							buffer2[(int)num2].m_flags = buffer2[(int)num2].m_flags | TransportLine.Flags.CustomColor;
							this.m_blimpLine.Disable();
						}
					}
					else
					{
						this.m_monorailLine.Disable();
					}
				}
				else
				{
					this.m_ferryLine.Disable();
				}
			}
			else
			{
				Singleton<BuildingManager>.instance.m_helicopterStopBuilt.Disable();
			}
			int defaultLineVehicleIndex = this.GetDefaultLineVehicleIndex(lineID);
			if (defaultLineVehicleIndex >= 0)
			{
				this.m_transportLineVehicleSelectionIndices[lineID] = defaultLineVehicleIndex;
			}
			return true;
		}
		lineID = 0;
		return false;
	}

	// Token: 0x060044EB RID: 17643 RVA: 0x003202A0 File Offset: 0x0031E6A0
	public int GetDefaultLineVehicleIndex(ushort lineID)
	{
		string defaultLineVehicleName = this.GetDefaultLineVehicleName(lineID);
		return this.GetVehicleIndex(defaultLineVehicleName);
	}

	// Token: 0x060044EC RID: 17644 RVA: 0x003202BC File Offset: 0x0031E6BC
	public int GetVehicleIndex(string vehicleName)
	{
		if (vehicleName == null)
		{
			return -1;
		}
		int num = PrefabCollection<VehicleInfo>.PrefabCount();
		for (int i = 0; i < num; i++)
		{
			VehicleInfo prefab = PrefabCollection<VehicleInfo>.GetPrefab((uint)i);
			if (prefab != null && PrefabCollection<VehicleInfo>.PrefabName((uint)i) == vehicleName)
			{
				return i;
			}
		}
		return -1;
	}

	// Token: 0x060044ED RID: 17645 RVA: 0x0032030C File Offset: 0x0031E70C
	public string GetDefaultLineVehicleName(ushort lineId)
	{
		TransportInfo info = Singleton<TransportManager>.instance.m_lines.m_buffer[(int)lineId].Info;
		if (info == null)
		{
			return null;
		}
		ItemClass @class = info.m_class;
		TransportLine.DepotLevels depotLevels = Singleton<BuildingManager>.instance.GetDepotLevels(lineId);
		ItemClass.SubService subService = @class.m_subService;
		switch (subService)
		{
		case ItemClass.SubService.PublicTransportBus:
			if (depotLevels.levelCount == 1 && depotLevels.Includes(ItemClass.Level.Level2))
			{
				return "Biofuel Bus 01";
			}
			return "Bus";
		case ItemClass.SubService.PublicTransportMetro:
			if (depotLevels.levelCount >= 1 && depotLevels.Includes(ItemClass.Level.Level1))
			{
				return "Metro";
			}
			return null;
		case ItemClass.SubService.PublicTransportTrain:
			if (depotLevels.levelCount == 1 && depotLevels.Includes(ItemClass.Level.Level2))
			{
				return "Airport Train Engine";
			}
			if (depotLevels.levelCount >= 1 && depotLevels.Includes(ItemClass.Level.Level1))
			{
				return "Train Engine";
			}
			return null;
		case ItemClass.SubService.PublicTransportShip:
			if (@class.m_level == ItemClass.Level.Level2 && depotLevels.levelCount >= 1 && depotLevels.Includes(ItemClass.Level.Level2))
			{
				return "Ferry";
			}
			return null;
		case ItemClass.SubService.PublicTransportPlane:
			if (@class.m_level == ItemClass.Level.Level2 && depotLevels.levelCount >= 1 && depotLevels.Includes(ItemClass.Level.Level2))
			{
				return "Blimp";
			}
			if (@class.m_level == ItemClass.Level.Level3 && depotLevels.levelCount >= 1 && depotLevels.Includes(ItemClass.Level.Level3))
			{
				return "Passenger Helicopter";
			}
			return null;
		default:
			if (subService != ItemClass.SubService.PublicTransportTours)
			{
				if (subService != ItemClass.SubService.PublicTransportTrolleybus)
				{
					return null;
				}
				if (depotLevels.levelCount >= 1 && depotLevels.Includes(ItemClass.Level.Level1))
				{
					return "Trolleybus 01";
				}
				return null;
			}
			else
			{
				if (@class.m_level == ItemClass.Level.Level3 && depotLevels.levelCount >= 1 && depotLevels.Includes(ItemClass.Level.Level3))
				{
					return "Sightseeing Bus 01";
				}
				return null;
			}
			break;
		case ItemClass.SubService.PublicTransportTram:
			if (depotLevels.levelCount >= 1 && depotLevels.Includes(ItemClass.Level.Level1))
			{
				return "Tram";
			}
			return null;
		case ItemClass.SubService.PublicTransportMonorail:
			if (depotLevels.levelCount >= 1 && depotLevels.Includes(ItemClass.Level.Level1))
			{
				return "Monorail Front";
			}
			return null;
		}
	}

	// Token: 0x060044EE RID: 17646 RVA: 0x0032054D File Offset: 0x0031E94D
	public void ReleaseLine(ushort lineID)
	{
		if (this.m_transportLineVehicleSelectionIndices.ContainsKey(lineID))
		{
			this.m_transportLineVehicleSelectionIndices.Remove(lineID);
		}
		this.ReleaseLineImplementation(lineID, ref this.m_lines.m_buffer[(int)lineID]);
	}

	// Token: 0x060044EF RID: 17647 RVA: 0x00320588 File Offset: 0x0031E988
	private void ReleaseLineImplementation(ushort lineID, ref TransportLine data)
	{
		if (data.m_flags == TransportLine.Flags.None)
		{
			return;
		}
		while (!Monitor.TryEnter(this.m_lineMeshData, SimulationManager.SYNCHRONIZE_TIMEOUT))
		{
		}
		try
		{
			this.m_lineMeshData[(int)lineID] = null;
		}
		finally
		{
			Monitor.Exit(this.m_lineMeshData);
		}
		TransportInfo info = data.Info;
		InstanceID instanceID = default(InstanceID);
		instanceID.TransportLine = lineID;
		Singleton<InstanceManager>.instance.ReleaseInstance(instanceID);
		if (info != null)
		{
			if (data.m_lineNumber != 0 && data.m_lineNumber == this.m_lineNumber[(int)info.m_transportType])
			{
				data.m_lineNumber = 0;
				ushort num = 0;
				for (int i = 1; i < 256; i++)
				{
					if (this.m_lines.m_buffer[i].m_flags != TransportLine.Flags.None && this.m_lines.m_buffer[i].Info.m_transportType == info.m_transportType && this.m_lines.m_buffer[i].m_lineNumber > num)
					{
						num = this.m_lines.m_buffer[i].m_lineNumber;
					}
				}
				this.m_lineNumber[(int)info.m_transportType] = num;
			}
			TransferManager.TransferReason transferReason = info.m_vehicleReason;
			if (transferReason != TransferManager.TransferReason.None)
			{
				TransferManager.TransferOffer transferOffer = default(TransferManager.TransferOffer);
				transferOffer.TransportLine = lineID;
				Singleton<TransferManager>.instance.RemoveIncomingOffer(info.m_vehicleReason, transferOffer);
			}
			transferReason = info.m_citizenReason;
			if (transferReason != TransferManager.TransferReason.None)
			{
				TransferManager.TransferOffer transferOffer2 = default(TransferManager.TransferOffer);
				transferOffer2.TransportLine = lineID;
				Singleton<TransferManager>.instance.RemoveOutgoingOffer(info.m_citizenReason, transferOffer2);
				if (info.m_citizenReason == TransferManager.TransferReason.Entertainment)
				{
					Singleton<TransferManager>.instance.RemoveOutgoingOffer(TransferManager.TransferReason.Entertainment, transferOffer2);
					Singleton<TransferManager>.instance.RemoveOutgoingOffer(TransferManager.TransferReason.EntertainmentB, transferOffer2);
					Singleton<TransferManager>.instance.RemoveOutgoingOffer(TransferManager.TransferReason.EntertainmentC, transferOffer2);
					Singleton<TransferManager>.instance.RemoveOutgoingOffer(TransferManager.TransferReason.EntertainmentD, transferOffer2);
				}
				else if (info.m_citizenReason == TransferManager.TransferReason.TouristA)
				{
					Singleton<TransferManager>.instance.RemoveOutgoingOffer(TransferManager.TransferReason.TouristA, transferOffer2);
					Singleton<TransferManager>.instance.RemoveOutgoingOffer(TransferManager.TransferReason.TouristB, transferOffer2);
					Singleton<TransferManager>.instance.RemoveOutgoingOffer(TransferManager.TransferReason.TouristC, transferOffer2);
					Singleton<TransferManager>.instance.RemoveOutgoingOffer(TransferManager.TransferReason.TouristD, transferOffer2);
				}
				else
				{
					Singleton<TransferManager>.instance.RemoveOutgoingOffer(info.m_citizenReason, transferOffer2);
				}
			}
		}
		if (data.m_vehicles != 0)
		{
			VehicleManager instance = Singleton<VehicleManager>.instance;
			ushort num2 = data.m_vehicles;
			int num3 = 0;
			while (num2 != 0)
			{
				ushort nextLineVehicle = instance.m_vehicles.m_buffer[(int)num2].m_nextLineVehicle;
				VehicleInfo info2 = instance.m_vehicles.m_buffer[(int)num2].Info;
				info2.m_vehicleAI.SetTransportLine(num2, ref instance.m_vehicles.m_buffer[(int)num2], 0);
				num2 = nextLineVehicle;
				if (++num3 > 16384)
				{
					CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
					break;
				}
			}
		}
		if (data.m_stops != 0)
		{
			NetManager instance2 = Singleton<NetManager>.instance;
			ushort num4 = data.m_stops;
			ushort num5 = 0;
			if (num4 != 0 && data.Complete)
			{
				num5 = TransportLine.GetPrevStop(num4);
			}
			int num6 = 0;
			while (num4 != 0 && (instance2.m_nodes.m_buffer[(int)num4].m_flags & (NetNode.Flags.Created | NetNode.Flags.Deleted)) == NetNode.Flags.Created && instance2.m_nodes.m_buffer[(int)num4].m_transportLine == lineID)
			{
				ushort nextStop = TransportLine.GetNextStop(num4);
				if (nextStop != 0 && num5 != 0)
				{
					Vector3 position = instance2.m_nodes.m_buffer[(int)num4].m_position;
					data.AddWaveEvent(position, position, -2, 0);
				}
				else if (num5 != 0 || nextStop != 0)
				{
					Vector3 position2 = instance2.m_nodes.m_buffer[(int)num4].m_position;
					data.AddWaveEvent(position2, position2, -1, 0);
				}
				data.ReleaseNode(num4);
				num5 = num4;
				num4 = nextStop;
				if (++num6 >= 32768)
				{
					CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
					break;
				}
			}
			data.m_stops = 0;
		}
		data.m_flags &= ~TransportLine.Flags.Complete;
		data.CheckCompletionMilestone();
		data.m_flags = TransportLine.Flags.None;
		this.m_lines.ReleaseItem(lineID);
		this.m_lineCount = (int)(this.m_lines.ItemCount() - 1U);
	}

	// Token: 0x060044F0 RID: 17648 RVA: 0x00320A0C File Offset: 0x0031EE0C
	public void SetPatchDirty(ushort segmentID, TransportInfo info, bool canCreate)
	{
		NetManager instance = Singleton<NetManager>.instance;
		ushort startNode = instance.m_segments.m_buffer[(int)segmentID].m_startNode;
		ushort endNode = instance.m_segments.m_buffer[(int)segmentID].m_endNode;
		Vector3 position = instance.m_nodes.m_buffer[(int)startNode].m_position;
		Vector3 position2 = instance.m_nodes.m_buffer[(int)endNode].m_position;
		Vector3 vector = (position + position2) * 0.5f;
		float num = 17280f;
		float num2 = 1920f;
		int num3 = Mathf.Clamp((int)((vector.x + num * 0.5f) / num2), 0, 8);
		int num4 = Mathf.Clamp((int)((vector.z + num * 0.5f) / num2), 0, 8);
		this.SetPatchDirty(num3, num4, info, canCreate);
		if (info.m_usePathNodes)
		{
			int num5 = Mathf.Clamp((int)((position.x + num * 0.5f) / num2), 0, 8);
			int num6 = Mathf.Clamp((int)((position.z + num * 0.5f) / num2), 0, 8);
			if (num5 != num3 || num6 != num4)
			{
				this.SetPatchDirty(num5, num6, info, canCreate);
			}
			for (int i = 0; i < 8; i++)
			{
				ushort segment = instance.m_nodes.m_buffer[(int)startNode].GetSegment(i);
				if (segment != 0 && segment != segmentID)
				{
					ushort startNode2 = instance.m_segments.m_buffer[(int)segment].m_startNode;
					ushort endNode2 = instance.m_segments.m_buffer[(int)segment].m_endNode;
					Vector3 position3 = instance.m_nodes.m_buffer[(int)startNode2].m_position;
					Vector3 position4 = instance.m_nodes.m_buffer[(int)endNode2].m_position;
					Vector3 vector2 = (position3 + position4) * 0.5f;
					int num7 = Mathf.Clamp((int)((vector2.x + num * 0.5f) / num2), 0, 8);
					int num8 = Mathf.Clamp((int)((vector2.z + num * 0.5f) / num2), 0, 8);
					if (num7 != num5 || num8 != num6)
					{
						this.SetPatchDirty(num7, num8, info, canCreate);
					}
				}
			}
			int num9 = Mathf.Clamp((int)((position2.x + num * 0.5f) / num2), 0, 8);
			int num10 = Mathf.Clamp((int)((position2.z + num * 0.5f) / num2), 0, 8);
			if (num9 != num3 || num10 != num4)
			{
				this.SetPatchDirty(num9, num10, info, canCreate);
			}
			for (int j = 0; j < 8; j++)
			{
				ushort segment2 = instance.m_nodes.m_buffer[(int)endNode].GetSegment(j);
				if (segment2 != 0 && segment2 != segmentID)
				{
					ushort startNode3 = instance.m_segments.m_buffer[(int)segment2].m_startNode;
					ushort endNode3 = instance.m_segments.m_buffer[(int)segment2].m_endNode;
					Vector3 position5 = instance.m_nodes.m_buffer[(int)startNode3].m_position;
					Vector3 position6 = instance.m_nodes.m_buffer[(int)endNode3].m_position;
					Vector3 vector3 = (position5 + position6) * 0.5f;
					int num11 = Mathf.Clamp((int)((vector3.x + num * 0.5f) / num2), 0, 8);
					int num12 = Mathf.Clamp((int)((vector3.z + num * 0.5f) / num2), 0, 8);
					if (num11 != num9 || num12 != num10)
					{
						this.SetPatchDirty(num11, num12, info, canCreate);
					}
				}
			}
		}
	}

	// Token: 0x060044F1 RID: 17649 RVA: 0x00320DC0 File Offset: 0x0031F1C0
	public void SetPatchDirty(int x, int z, TransportInfo info, bool canCreate)
	{
		int num = z * 9 + x;
		TransportPatch transportPatch = null;
		TransportPatch transportPatch2 = this.m_patches[num];
		int num2 = 0;
		while (transportPatch2 != null)
		{
			if (transportPatch2.m_info == info)
			{
				transportPatch2.m_isDirty = true;
				this.m_patchesDirty = true;
				return;
			}
			transportPatch = transportPatch2;
			transportPatch2 = transportPatch2.m_nextPatch;
			if (++num2 >= 100)
			{
				CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
				break;
			}
		}
		if (canCreate && (Singleton<ToolManager>.instance.m_properties.m_mode & info.m_pathVisibility) != ItemClass.Availability.None)
		{
			transportPatch2 = new TransportPatch(x, z, info);
			transportPatch2.m_isDirty = true;
			this.m_patchesDirty = true;
			if (transportPatch != null)
			{
				transportPatch.m_nextPatch = transportPatch2;
			}
			else
			{
				this.m_patches[num] = transportPatch2;
			}
		}
	}

	// Token: 0x060044F2 RID: 17650 RVA: 0x00320E8A File Offset: 0x0031F28A
	public void UpdateLine(ushort lineID)
	{
		this.m_updatedLines[lineID >> 6] |= 1UL << (int)lineID;
		this.m_linesUpdated = true;
	}

	// Token: 0x060044F3 RID: 17651 RVA: 0x00320EB0 File Offset: 0x0031F2B0
	public bool RayCast(Ray ray, float rayLength, int transportTypes, out Vector3 hit, out ushort lineIndex, out int stopIndex, out int segmentIndex)
	{
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		float num5 = 16f;
		float num6 = 9f;
		Vector3 vector = Vector3.zero;
		Vector3 vector2 = Vector3.zero;
		Vector3 origin = ray.origin;
		Vector3 normalized = ray.direction.normalized;
		Vector3 vector3 = ray.origin + normalized * rayLength;
		Segment3 segment;
		segment..ctor(origin, vector3);
		NetManager instance = Singleton<NetManager>.instance;
		for (int i = 1; i < 256; i++)
		{
			TransportLine.Flags flags = this.m_lines.m_buffer[i].m_flags;
			if ((flags & (TransportLine.Flags.Created | TransportLine.Flags.Temporary)) == TransportLine.Flags.Created && (flags & (TransportLine.Flags.Hidden | TransportLine.Flags.Selected)) != TransportLine.Flags.Hidden)
			{
				TransportInfo info = this.m_lines.m_buffer[i].Info;
				if ((transportTypes & (1 << (int)info.m_transportType)) != 0 && this.m_lines.m_buffer[i].m_bounds.IntersectRay(ray))
				{
					TransportManager.LineSegment[] array = this.m_lineSegments[i];
					Bezier3[] array2 = this.m_lineCurves[i];
					ushort stops = this.m_lines.m_buffer[i].m_stops;
					ushort num7 = stops;
					int num8 = 0;
					while (num7 != 0)
					{
						Vector3 position = instance.m_nodes.m_buffer[(int)num7].m_position;
						float num9 = Line3.DistanceSqr(ray.direction, ray.origin - position);
						if (num9 < num5)
						{
							num = i;
							num3 = num8;
							num5 = num9;
							vector = position;
						}
						if (array.Length > num8 && array[num8].m_bounds.IntersectRay(ray))
						{
							int curveStart = array[num8].m_curveStart;
							int curveEnd = array[num8].m_curveEnd;
							for (int j = curveStart; j < curveEnd; j++)
							{
								Vector3 vector4 = array2[j].Min() - new Vector3(3f, 3f, 3f);
								Vector3 vector5 = array2[j].Max() + new Vector3(3f, 3f, 3f);
								Bounds bounds = default(Bounds);
								bounds.SetMinMax(vector4, vector5);
								if (bounds.IntersectRay(ray))
								{
									float num10;
									float num11;
									num9 = array2[j].DistanceSqr(segment, ref num10, ref num11);
									if (num9 < num6)
									{
										num2 = i;
										num4 = num8;
										num6 = num9;
										vector2 = array2[j].Position(num10);
									}
								}
							}
						}
						num7 = TransportLine.GetNextStop(num7);
						if (num7 == stops)
						{
							break;
						}
						if (++num8 >= 32768)
						{
							CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
							break;
						}
					}
				}
			}
		}
		if (num != 0)
		{
			hit = vector;
			lineIndex = (ushort)num;
			stopIndex = num3;
			segmentIndex = -1;
			return true;
		}
		if (num2 != 0)
		{
			hit = vector2;
			lineIndex = (ushort)num2;
			stopIndex = -1;
			segmentIndex = num4;
			return true;
		}
		hit = Vector3.zero;
		lineIndex = 0;
		stopIndex = -1;
		segmentIndex = -1;
		return false;
	}

	// Token: 0x060044F4 RID: 17652 RVA: 0x003211EC File Offset: 0x0031F5EC
	public void UpdateLinesNow()
	{
		if (this.m_linesUpdated)
		{
			this.m_linesUpdated = false;
			int num = this.m_updatedLines.Length;
			for (int i = 0; i < num; i++)
			{
				ulong num2 = this.m_updatedLines[i];
				if (num2 != 0UL)
				{
					for (int j = 0; j < 64; j++)
					{
						if ((num2 & (1UL << j)) != 0UL)
						{
							ushort num3 = (ushort)((i << 6) | j);
							if (this.m_lines.m_buffer[(int)num3].m_flags != TransportLine.Flags.None)
							{
								if (this.m_lines.m_buffer[(int)num3].UpdatePaths(num3) && this.m_lines.m_buffer[(int)num3].UpdateMeshData(num3))
								{
									num2 &= ~(1UL << j);
								}
							}
							else
							{
								num2 &= ~(1UL << j);
							}
						}
					}
					this.m_updatedLines[i] = num2;
					if (num2 != 0UL)
					{
						this.m_linesUpdated = true;
					}
				}
			}
		}
	}

	// Token: 0x060044F5 RID: 17653 RVA: 0x003212F0 File Offset: 0x0031F6F0
	protected override void SimulationStepImpl(int subStep)
	{
		this.UpdateLinesNow();
		if (this.m_patchesDirty)
		{
			this.m_patchesDirty = false;
			int num = this.m_patches.Length;
			for (int i = 0; i < num; i++)
			{
				TransportPatch transportPatch = this.m_patches[i];
				int num2 = 0;
				while (transportPatch != null)
				{
					if (transportPatch.m_isDirty)
					{
						transportPatch.UpdateMeshData();
					}
					transportPatch = transportPatch.m_nextPatch;
					if (++num2 >= 100)
					{
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}
			}
		}
		if (subStep != 0)
		{
			int num3 = (int)(Singleton<SimulationManager>.instance.m_currentFrameIndex & 255U);
			int num4 = num3;
			int num5 = num3 + 1 - 1;
			for (int j = num4; j <= num5; j++)
			{
				TransportLine.Flags flags = this.m_lines.m_buffer[j].m_flags;
				if ((flags & (TransportLine.Flags.Created | TransportLine.Flags.Temporary)) == TransportLine.Flags.Created)
				{
					this.m_lines.m_buffer[j].SimulationStep((ushort)j);
				}
			}
			if ((Singleton<SimulationManager>.instance.m_currentFrameIndex & 4095U) == 0U)
			{
				StatisticsManager instance = Singleton<StatisticsManager>.instance;
				StatisticBase statisticBase = instance.Acquire<StatisticArray>(StatisticType.AveragePassengers);
				for (int k = 0; k < 17; k++)
				{
					this.m_passengers[k].Update();
					this.m_passengers[k].Reset();
					statisticBase.Acquire<StatisticInt32>(k, 17).Set((int)(this.m_passengers[k].m_residentPassengers.m_averageCount + this.m_passengers[k].m_touristPassengers.m_averageCount));
				}
			}
		}
		if (subStep <= 1)
		{
			int num6 = (int)(Singleton<SimulationManager>.instance.m_currentTickIndex & 1023U);
			int num7 = num6 * PrefabCollection<TransportInfo>.PrefabCount() >> 10;
			int num8 = ((num6 + 1) * PrefabCollection<TransportInfo>.PrefabCount() >> 10) - 1;
			for (int l = num7; l <= num8; l++)
			{
				TransportInfo prefab = PrefabCollection<TransportInfo>.GetPrefab((uint)l);
				if (prefab != null)
				{
					MilestoneInfo unlockMilestone = prefab.m_UnlockMilestone;
					if (unlockMilestone != null)
					{
						Singleton<UnlockManager>.instance.CheckMilestone(unlockMilestone, false, false);
					}
				}
			}
		}
	}

	// Token: 0x060044F6 RID: 17654 RVA: 0x0032151D File Offset: 0x0031F91D
	public bool TryGetSelectedLineVehicle(ushort lineID, out int prefabIndex)
	{
		if (!this.m_transportLineVehicleSelectionIndices.TryGetValue(lineID, ref prefabIndex))
		{
			prefabIndex = -1;
			return false;
		}
		return true;
	}

	// Token: 0x060044F7 RID: 17655 RVA: 0x00321537 File Offset: 0x0031F937
	public void AssignSelectedLineVehicle(ushort lineID, int prefabIndex)
	{
		if (prefabIndex < 0)
		{
			if (this.m_transportLineVehicleSelectionIndices.ContainsKey(lineID))
			{
				this.m_transportLineVehicleSelectionIndices.Remove(lineID);
			}
		}
		else
		{
			this.m_transportLineVehicleSelectionIndices[lineID] = prefabIndex;
		}
	}

	// Token: 0x1400002B RID: 43
	// (add) Token: 0x060044F8 RID: 17656 RVA: 0x00321570 File Offset: 0x0031F970
	// (remove) Token: 0x060044F9 RID: 17657 RVA: 0x003215A8 File Offset: 0x0031F9A8
	[field: DebuggerBrowsable(0)]
	public event TransportManager.LineColorChangedHandler eventLineColorChanged;

	// Token: 0x1400002C RID: 44
	// (add) Token: 0x060044FA RID: 17658 RVA: 0x003215E0 File Offset: 0x0031F9E0
	// (remove) Token: 0x060044FB RID: 17659 RVA: 0x00321618 File Offset: 0x0031FA18
	[field: DebuggerBrowsable(0)]
	public event TransportManager.LineNameChangedHandler eventLineNameChanged;

	// Token: 0x060044FC RID: 17660 RVA: 0x00321650 File Offset: 0x0031FA50
	public IEnumerator<bool> SetLineColor(ushort lineID, Color color)
	{
		bool result = false;
		TransportLine.Flags flags = this.m_lines.m_buffer[(int)lineID].m_flags;
		if (lineID != 0 && flags != TransportLine.Flags.None)
		{
			TransportInfo info = this.m_lines.m_buffer[(int)lineID].Info;
			if (color == Singleton<TransportManager>.instance.m_properties.m_transportColors[(int)info.m_transportType])
			{
				TransportLine[] buffer = this.m_lines.m_buffer;
				ushort lineID2 = lineID;
				buffer[(int)lineID2].m_flags = buffer[(int)lineID2].m_flags & ~TransportLine.Flags.CustomColor;
			}
			else
			{
				TransportLine[] buffer2 = this.m_lines.m_buffer;
				ushort lineID3 = lineID;
				buffer2[(int)lineID3].m_flags = buffer2[(int)lineID3].m_flags | TransportLine.Flags.CustomColor;
				this.m_lines.m_buffer[(int)lineID].m_color = color;
			}
			result = true;
			if (this.eventLineColorChanged != null)
			{
				ThreadHelper.dispatcher.Dispatch(delegate
				{
					this.eventLineColorChanged(lineID);
				});
			}
		}
		yield return result;
		yield break;
	}

	// Token: 0x060044FD RID: 17661 RVA: 0x0032167C File Offset: 0x0031FA7C
	public IEnumerator<bool> SetLineName(ushort lineID, string name)
	{
		bool result = false;
		TransportLine.Flags flags = this.m_lines.m_buffer[(int)lineID].m_flags;
		if (lineID != 0 && flags != TransportLine.Flags.None)
		{
			if (!StringExtensions.IsNullOrWhiteSpace(name) && name != this.GenerateName(lineID))
			{
				this.m_lines.m_buffer[(int)lineID].m_flags = flags | TransportLine.Flags.CustomName;
				InstanceID instanceID = default(InstanceID);
				instanceID.TransportLine = lineID;
				Singleton<InstanceManager>.instance.SetName(instanceID, name);
				Singleton<GuideManager>.instance.m_renameNotUsed.Disable();
			}
			else if ((flags & TransportLine.Flags.CustomName) != TransportLine.Flags.None)
			{
				this.m_lines.m_buffer[(int)lineID].m_flags = flags & ~TransportLine.Flags.CustomName;
				InstanceID instanceID2 = default(InstanceID);
				instanceID2.TransportLine = lineID;
				Singleton<InstanceManager>.instance.SetName(instanceID2, null);
			}
			result = true;
			if (this.eventLineNameChanged != null)
			{
				ThreadHelper.dispatcher.Dispatch(delegate
				{
					this.eventLineNameChanged(lineID);
				});
			}
		}
		yield return result;
		yield break;
	}

	// Token: 0x060044FE RID: 17662 RVA: 0x003216A8 File Offset: 0x0031FAA8
	public void CheckAllMilestones()
	{
		int num = PrefabCollection<TransportInfo>.PrefabCount();
		for (int i = 0; i < num; i++)
		{
			TransportInfo prefab = PrefabCollection<TransportInfo>.GetPrefab((uint)i);
			if (prefab != null)
			{
				MilestoneInfo unlockMilestone = prefab.m_UnlockMilestone;
				if (unlockMilestone != null)
				{
					Singleton<UnlockManager>.instance.CheckMilestone(unlockMilestone, false, false);
				}
			}
		}
	}

	// Token: 0x060044FF RID: 17663 RVA: 0x003216F8 File Offset: 0x0031FAF8
	public void CheckCompletionMilestones()
	{
		for (int i = 1; i < 256; i++)
		{
			if (this.m_lines.m_buffer[i].m_flags != TransportLine.Flags.None)
			{
				this.m_lines.m_buffer[i].CheckCompletionMilestone();
			}
		}
	}

	// Token: 0x06004500 RID: 17664 RVA: 0x0032174C File Offset: 0x0031FB4C
	public void CheckTransportLineVehicles()
	{
		ushort num = 0;
		while ((uint)num < this.m_lines.m_size)
		{
			ushort vehicles = this.m_lines.m_buffer[(int)num].m_vehicles;
			if (vehicles != 0)
			{
				VehicleInfo lineVehicle = this.m_lines.m_buffer[(int)num].GetLineVehicle(num);
				VehicleInfo info = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[(int)vehicles].Info;
				if (lineVehicle != null && info != null)
				{
					if (lineVehicle.m_class.m_level != info.m_class.m_level && Singleton<BuildingManager>.instance.GetDepotLevels(num).Includes(lineVehicle.m_class.m_level))
					{
						this.m_lines.m_buffer[(int)num].ReleaseLineVehicles();
					}
				}
			}
			num += 1;
		}
	}

	// Token: 0x06004501 RID: 17665 RVA: 0x00321834 File Offset: 0x0031FC34
	public override void EarlyUpdateData()
	{
		base.EarlyUpdateData();
		int num = PrefabCollection<TransportInfo>.PrefabCount();
		for (int i = 0; i < num; i++)
		{
			TransportInfo prefab = PrefabCollection<TransportInfo>.GetPrefab((uint)i);
			if (prefab != null)
			{
				MilestoneInfo unlockMilestone = prefab.m_UnlockMilestone;
				if (unlockMilestone != null)
				{
					unlockMilestone.ResetWrittenStatus();
				}
			}
		}
	}

	// Token: 0x06004502 RID: 17666 RVA: 0x00321880 File Offset: 0x0031FC80
	public override void UpdateData(SimulationManager.UpdateMode mode)
	{
		Singleton<LoadingManager>.instance.m_loadingProfilerSimulation.BeginLoading("TransportManager.UpdateData");
		base.UpdateData(mode);
		for (int i = 1; i < 256; i++)
		{
			if (this.m_lines.m_buffer[i].m_flags != TransportLine.Flags.None)
			{
				TransportInfo info = this.m_lines.m_buffer[i].Info;
				if (info == null || (this.m_lines.m_buffer[i].m_flags & TransportLine.Flags.Invalid) != TransportLine.Flags.None)
				{
					this.ReleaseLine((ushort)i);
				}
				else if (this.m_lines.m_buffer[i].m_building != 0)
				{
					ushort building = this.m_lines.m_buffer[i].m_building;
					BuildingManager instance = Singleton<BuildingManager>.instance;
					if ((instance.m_buildings.m_buffer[(int)building].m_flags & (Building.Flags.Created | Building.Flags.Deleted)) != Building.Flags.Created || instance.m_buildings.m_buffer[(int)building].Info == null)
					{
						this.ReleaseLine((ushort)i);
					}
				}
			}
		}
		int num = PrefabCollection<TransportInfo>.PrefabCount();
		this.m_infoCount = num;
		for (int j = 0; j < num; j++)
		{
			TransportInfo prefab = PrefabCollection<TransportInfo>.GetPrefab((uint)j);
			if (prefab != null && prefab.m_UnlockMilestone != null)
			{
				switch (mode)
				{
				case SimulationManager.UpdateMode.NewMap:
				case SimulationManager.UpdateMode.LoadMap:
				case SimulationManager.UpdateMode.NewAsset:
				case SimulationManager.UpdateMode.LoadAsset:
					prefab.m_UnlockMilestone.Reset(false);
					break;
				case SimulationManager.UpdateMode.NewGameFromMap:
				case SimulationManager.UpdateMode.NewScenarioFromMap:
				case SimulationManager.UpdateMode.UpdateScenarioFromMap:
					prefab.m_UnlockMilestone.Reset(false);
					break;
				case SimulationManager.UpdateMode.LoadGame:
				case SimulationManager.UpdateMode.NewScenarioFromGame:
				case SimulationManager.UpdateMode.LoadScenario:
				case SimulationManager.UpdateMode.NewGameFromScenario:
				case SimulationManager.UpdateMode.UpdateScenarioFromGame:
					prefab.m_UnlockMilestone.Reset(true);
					break;
				}
			}
		}
		if (mode == SimulationManager.UpdateMode.NewGameFromMap || mode == SimulationManager.UpdateMode.NewScenarioFromMap || mode == SimulationManager.UpdateMode.UpdateScenarioFromMap || this.m_monorailLine == null)
		{
			this.m_monorailLine = new GenericGuide();
		}
		if (mode == SimulationManager.UpdateMode.NewGameFromMap || mode == SimulationManager.UpdateMode.NewScenarioFromMap || mode == SimulationManager.UpdateMode.UpdateScenarioFromMap || this.m_endOfLineStation == null)
		{
			this.m_endOfLineStation = new BuildingInstanceGuide();
		}
		if (mode == SimulationManager.UpdateMode.NewGameFromMap || mode == SimulationManager.UpdateMode.NewScenarioFromMap || mode == SimulationManager.UpdateMode.UpdateScenarioFromMap || this.m_ferryBusHub == null)
		{
			this.m_ferryBusHub = new BuildingInstanceGuide();
		}
		if (mode == SimulationManager.UpdateMode.NewGameFromMap || mode == SimulationManager.UpdateMode.NewScenarioFromMap || mode == SimulationManager.UpdateMode.UpdateScenarioFromMap || this.m_ferryLine == null)
		{
			this.m_ferryLine = new GenericGuide();
		}
		if (mode == SimulationManager.UpdateMode.NewGameFromMap || mode == SimulationManager.UpdateMode.NewScenarioFromMap || mode == SimulationManager.UpdateMode.UpdateScenarioFromMap || this.m_ferryDepot == null)
		{
			this.m_ferryDepot = new GenericGuide();
		}
		if (mode == SimulationManager.UpdateMode.NewGameFromMap || mode == SimulationManager.UpdateMode.NewScenarioFromMap || mode == SimulationManager.UpdateMode.UpdateScenarioFromMap || this.m_blimpLine == null)
		{
			this.m_blimpLine = new GenericGuide();
		}
		if (mode == SimulationManager.UpdateMode.NewGameFromMap || mode == SimulationManager.UpdateMode.NewScenarioFromMap || mode == SimulationManager.UpdateMode.UpdateScenarioFromMap || this.m_blimpDepot == null)
		{
			this.m_blimpDepot = new GenericGuide();
		}
		this.m_transportLineVehicleSelectionIndices.Clear();
		int num2 = PrefabCollection<VehicleInfo>.PrefabCount();
		for (int k = 0; k < num2; k++)
		{
			VehicleInfo prefab2 = PrefabCollection<VehicleInfo>.GetPrefab((uint)k);
			if (prefab2 != null)
			{
				string text = PrefabCollection<VehicleInfo>.PrefabName((uint)k);
				if (this.m_transportLineVehicleSelection.ContainsValue(text))
				{
					foreach (KeyValuePair<ushort, string> keyValuePair in this.m_transportLineVehicleSelection)
					{
						ushort key = keyValuePair.Key;
						string value = keyValuePair.Value;
						if (value == text || (value == "#DEFAULT#" && this.GetDefaultLineVehicleName(key) == text))
						{
							if (this.m_lines.m_buffer[(int)key].IsCompatibleVehicle(key, prefab2))
							{
								this.m_transportLineVehicleSelectionIndices[key] = k;
							}
							else
							{
								this.m_transportLineVehicleSelectionIndices[key] = -1;
							}
						}
					}
				}
			}
		}
		foreach (KeyValuePair<ushort, string> keyValuePair2 in this.m_transportLineVehicleSelection)
		{
			int defaultLineVehicleIndex;
			if (!this.m_transportLineVehicleSelectionIndices.TryGetValue(keyValuePair2.Key, ref defaultLineVehicleIndex))
			{
				defaultLineVehicleIndex = this.GetDefaultLineVehicleIndex(keyValuePair2.Key);
				if (defaultLineVehicleIndex >= 0)
				{
					this.m_transportLineVehicleSelectionIndices[keyValuePair2.Key] = defaultLineVehicleIndex;
				}
			}
			else if (defaultLineVehicleIndex == -1)
			{
				this.m_transportLineVehicleSelectionIndices.Remove(keyValuePair2.Key);
			}
		}
		Singleton<LoadingManager>.instance.m_loadingProfilerSimulation.EndLoading();
	}

	// Token: 0x06004503 RID: 17667 RVA: 0x00321D74 File Offset: 0x00320174
	public override void GetData(FastList<IDataContainer> data)
	{
		base.GetData(data);
		data.Add(new TransportManager.Data());
	}

	// Token: 0x06004504 RID: 17668 RVA: 0x00321D88 File Offset: 0x00320188
	public string GetLineName(ushort lineID)
	{
		if (this.m_lines.m_buffer[(int)lineID].m_flags != TransportLine.Flags.None)
		{
			string text = null;
			if ((this.m_lines.m_buffer[(int)lineID].m_flags & TransportLine.Flags.CustomName) != TransportLine.Flags.None)
			{
				InstanceID instanceID = default(InstanceID);
				instanceID.TransportLine = lineID;
				text = Singleton<InstanceManager>.instance.GetName(instanceID);
			}
			if (text == null)
			{
				text = this.GenerateName(lineID);
			}
			return text;
		}
		return null;
	}

	// Token: 0x06004505 RID: 17669 RVA: 0x00321E00 File Offset: 0x00320200
	public Color GetLineColor(ushort lineID)
	{
		if (this.m_lines.m_buffer[(int)lineID].m_flags == TransportLine.Flags.None)
		{
			return Color.white;
		}
		if ((this.m_lines.m_buffer[(int)lineID].m_flags & TransportLine.Flags.CustomColor) != TransportLine.Flags.None)
		{
			return this.m_lines.m_buffer[(int)lineID].m_color;
		}
		return Singleton<TransportManager>.instance.m_properties.m_transportColors[(int)this.m_lines.m_buffer[(int)lineID].Info.m_transportType];
	}

	// Token: 0x06004506 RID: 17670 RVA: 0x00321EA0 File Offset: 0x003202A0
	private string GenerateName(ushort lineID)
	{
		string text = null;
		TransportInfo info = this.m_lines.m_buffer[(int)lineID].Info;
		if (info != null)
		{
			string text2 = PrefabCollection<TransportInfo>.PrefabName((uint)info.m_prefabDataIndex);
			string text3 = Locale.Get("TRANSPORT_LINE_PATTERN", text2);
			return StringUtils.SafeFormat(text3, this.m_lines.m_buffer[(int)lineID].m_lineNumber);
		}
		if (text == null)
		{
			text = "Invalid";
		}
		return text;
	}

	// Token: 0x06004507 RID: 17671 RVA: 0x00321F13 File Offset: 0x00320313
	public bool TransportTypeLoaded(TransportInfo.TransportType type)
	{
		return this.m_transportTypeLoaded[(int)type] != null;
	}

	// Token: 0x06004508 RID: 17672 RVA: 0x00321F23 File Offset: 0x00320323
	public TransportInfo GetTransportInfo(TransportInfo.TransportType type)
	{
		return this.m_transportTypeLoaded[(int)type];
	}

	// Token: 0x06004509 RID: 17673 RVA: 0x00321F30 File Offset: 0x00320330
	public bool TransportServiceLoaded(ItemClass.SubService subService)
	{
		switch (subService)
		{
		case ItemClass.SubService.PublicTransportBus:
			return this.TransportTypeLoaded(TransportInfo.TransportType.Bus);
		case ItemClass.SubService.PublicTransportMetro:
			return this.TransportTypeLoaded(TransportInfo.TransportType.Metro);
		case ItemClass.SubService.PublicTransportTrain:
			return this.TransportTypeLoaded(TransportInfo.TransportType.Train);
		case ItemClass.SubService.PublicTransportShip:
			return this.TransportTypeLoaded(TransportInfo.TransportType.Ship);
		case ItemClass.SubService.PublicTransportPlane:
			return this.TransportTypeLoaded(TransportInfo.TransportType.Airplane);
		case ItemClass.SubService.PublicTransportTaxi:
			return this.TransportTypeLoaded(TransportInfo.TransportType.Taxi);
		case ItemClass.SubService.PublicTransportTram:
			return this.TransportTypeLoaded(TransportInfo.TransportType.Tram);
		default:
			return subService != ItemClass.SubService.PublicTransportTrolleybus || this.TransportTypeLoaded(TransportInfo.TransportType.Trolleybus);
		case ItemClass.SubService.PublicTransportMonorail:
			return this.TransportTypeLoaded(TransportInfo.TransportType.Monorail);
		case ItemClass.SubService.PublicTransportCableCar:
			return this.TransportTypeLoaded(TransportInfo.TransportType.CableCar);
		case ItemClass.SubService.PublicTransportTours:
			return this.TransportTypeLoaded(TransportInfo.TransportType.TouristBus);
		case ItemClass.SubService.PublicTransportPost:
			return this.TransportTypeLoaded(TransportInfo.TransportType.Post);
		}
	}

	// Token: 0x0600450A RID: 17674 RVA: 0x00322004 File Offset: 0x00320404
	string ISimulationManager.GetName()
	{
		return base.GetName();
	}

	// Token: 0x0600450B RID: 17675 RVA: 0x0032200C File Offset: 0x0032040C
	ThreadProfiler ISimulationManager.GetSimulationProfiler()
	{
		return base.GetSimulationProfiler();
	}

	// Token: 0x0600450C RID: 17676 RVA: 0x00322014 File Offset: 0x00320414
	void ISimulationManager.SimulationStep(int subStep)
	{
		base.SimulationStep(subStep);
	}

	// Token: 0x0600450D RID: 17677 RVA: 0x0032201D File Offset: 0x0032041D
	string IRenderableManager.GetName()
	{
		return base.GetName();
	}

	// Token: 0x0600450E RID: 17678 RVA: 0x00322025 File Offset: 0x00320425
	DrawCallData IRenderableManager.GetDrawCallData()
	{
		return base.GetDrawCallData();
	}

	// Token: 0x0600450F RID: 17679 RVA: 0x0032202D File Offset: 0x0032042D
	void IRenderableManager.BeginRendering(RenderManager.CameraInfo cameraInfo)
	{
		base.BeginRendering(cameraInfo);
	}

	// Token: 0x06004510 RID: 17680 RVA: 0x00322036 File Offset: 0x00320436
	void IRenderableManager.EndRendering(RenderManager.CameraInfo cameraInfo)
	{
		base.EndRendering(cameraInfo);
	}

	// Token: 0x06004511 RID: 17681 RVA: 0x0032203F File Offset: 0x0032043F
	void IRenderableManager.BeginOverlay(RenderManager.CameraInfo cameraInfo)
	{
		base.BeginOverlay(cameraInfo);
	}

	// Token: 0x06004512 RID: 17682 RVA: 0x00322048 File Offset: 0x00320448
	void IRenderableManager.EndOverlay(RenderManager.CameraInfo cameraInfo)
	{
		base.EndOverlay(cameraInfo);
	}

	// Token: 0x06004513 RID: 17683 RVA: 0x00322051 File Offset: 0x00320451
	void IRenderableManager.UndergroundOverlay(RenderManager.CameraInfo cameraInfo)
	{
		base.UndergroundOverlay(cameraInfo);
	}

	// Token: 0x04004DB0 RID: 19888
	public const int MAX_LINE_COUNT = 256;

	// Token: 0x04004DB1 RID: 19889
	public int m_lineCount;

	// Token: 0x04004DB2 RID: 19890
	public int m_infoCount;

	// Token: 0x04004DB3 RID: 19891
	[NonSerialized]
	public Array16<TransportLine> m_lines;

	// Token: 0x04004DB4 RID: 19892
	[NonSerialized]
	public Mesh[][] m_lineMeshes;

	// Token: 0x04004DB5 RID: 19893
	[NonSerialized]
	public RenderGroup.MeshData[][] m_lineMeshData;

	// Token: 0x04004DB6 RID: 19894
	[NonSerialized]
	public TransportManager.LineSegment[][] m_lineSegments;

	// Token: 0x04004DB7 RID: 19895
	[NonSerialized]
	public Bezier3[][] m_lineCurves;

	// Token: 0x04004DB8 RID: 19896
	[NonSerialized]
	public ulong[] m_updatedLines;

	// Token: 0x04004DB9 RID: 19897
	[NonSerialized]
	public bool m_linesUpdated;

	// Token: 0x04004DBA RID: 19898
	[NonSerialized]
	public int m_metroLayer;

	// Token: 0x04004DBB RID: 19899
	[NonSerialized]
	public MaterialPropertyBlock m_materialBlock;

	// Token: 0x04004DBC RID: 19900
	[NonSerialized]
	public int ID_Color;

	// Token: 0x04004DBD RID: 19901
	[NonSerialized]
	public int ID_StartOffset;

	// Token: 0x04004DBE RID: 19902
	[NonSerialized]
	public Material m_patchMaterial;

	// Token: 0x04004DBF RID: 19903
	[NonSerialized]
	public TransportPassengerData[] m_passengers;

	// Token: 0x04004DC0 RID: 19904
	[GuideMetaData(81)]
	[NonSerialized]
	public GenericGuide m_monorailLine;

	// Token: 0x04004DC1 RID: 19905
	[GuideMetaData(83)]
	[NonSerialized]
	public BuildingInstanceGuide m_endOfLineStation;

	// Token: 0x04004DC2 RID: 19906
	[GuideMetaData(84)]
	[NonSerialized]
	public BuildingInstanceGuide m_ferryBusHub;

	// Token: 0x04004DC3 RID: 19907
	[GuideMetaData(87)]
	[NonSerialized]
	public GenericGuide m_ferryLine;

	// Token: 0x04004DC4 RID: 19908
	[GuideMetaData(88)]
	[NonSerialized]
	public GenericGuide m_ferryDepot;

	// Token: 0x04004DC5 RID: 19909
	[GuideMetaData(89)]
	[NonSerialized]
	public GenericGuide m_blimpLine;

	// Token: 0x04004DC6 RID: 19910
	[GuideMetaData(90)]
	[NonSerialized]
	public GenericGuide m_blimpDepot;

	// Token: 0x04004DC7 RID: 19911
	private ushort[] m_lineNumber;

	// Token: 0x04004DC8 RID: 19912
	private TransportInfo[] m_transportTypeLoaded;

	// Token: 0x04004DC9 RID: 19913
	private int m_linesVisible;

	// Token: 0x04004DCA RID: 19914
	private bool m_tunnelsVisible;

	// Token: 0x04004DCB RID: 19915
	private bool m_tunnelsVisibleInfo;

	// Token: 0x04004DCC RID: 19916
	private Camera m_undergroundCamera;

	// Token: 0x04004DCD RID: 19917
	private TransportPatch[] m_patches;

	// Token: 0x04004DCE RID: 19918
	private bool m_patchesDirty;

	// Token: 0x04004DCF RID: 19919
	private Dictionary<ushort, string> m_transportLineVehicleSelection;

	// Token: 0x04004DD0 RID: 19920
	private Dictionary<ushort, int> m_transportLineVehicleSelectionIndices;

	// Token: 0x020005EB RID: 1515
	public struct LineSegment
	{
		// Token: 0x04004DD3 RID: 19923
		public Bounds m_bounds;

		// Token: 0x04004DD4 RID: 19924
		public int m_curveStart;

		// Token: 0x04004DD5 RID: 19925
		public int m_curveEnd;
	}

	// Token: 0x020005EC RID: 1516
	// (Invoke) Token: 0x06004515 RID: 17685
	public delegate void LineColorChangedHandler(ushort id);

	// Token: 0x020005ED RID: 1517
	// (Invoke) Token: 0x06004519 RID: 17689
	public delegate void LineNameChangedHandler(ushort id);

	// Token: 0x020005EE RID: 1518
	public class Data : IDataContainer
	{
		// Token: 0x0600451D RID: 17693 RVA: 0x00322064 File Offset: 0x00320464
		public void Serialize(DataSerializer s)
		{
			Singleton<LoadingManager>.instance.m_loadingProfilerSimulation.BeginSerialize(s, "TransportManager");
			ToolManager instance = Singleton<ToolManager>.instance;
			TransportManager instance2 = Singleton<TransportManager>.instance;
			TransportLine[] buffer = instance2.m_lines.m_buffer;
			int num = buffer.Length;
			instance2.m_transportLineVehicleSelection.Clear();
			foreach (KeyValuePair<ushort, int> keyValuePair in instance2.m_transportLineVehicleSelectionIndices)
			{
				VehicleInfo prefab = PrefabCollection<VehicleInfo>.GetPrefab((uint)keyValuePair.Value);
				if (prefab != null)
				{
					string text = PrefabCollection<VehicleInfo>.PrefabName((uint)keyValuePair.Value);
					instance2.m_transportLineVehicleSelection[keyValuePair.Key] = text;
				}
			}
			EncodedArray.UInt @uint = EncodedArray.UInt.BeginWrite(s);
			for (int i = 1; i < num; i++)
			{
				@uint.Write((uint)buffer[i].m_flags);
			}
			@uint.EndWrite();
			int num2 = PrefabCollection<TransportInfo>.PrefabCount();
			uint num3 = 0U;
			if ((instance.m_properties.m_mode & ItemClass.Availability.Game) != ItemClass.Availability.None)
			{
				for (int j = 0; j < num2; j++)
				{
					TransportInfo prefab2 = PrefabCollection<TransportInfo>.GetPrefab((uint)j);
					if (prefab2 != null && prefab2.m_UnlockMilestone != null && prefab2.m_prefabDataIndex != -1)
					{
						num3 += 1U;
					}
				}
			}
			s.WriteUInt16(num3);
			try
			{
				PrefabCollection<TransportInfo>.BeginSerialize(s);
				for (int k = 1; k < num; k++)
				{
					if (buffer[k].m_flags != TransportLine.Flags.None)
					{
						PrefabCollection<TransportInfo>.Serialize((uint)buffer[k].m_infoIndex);
					}
				}
				if ((instance.m_properties.m_mode & ItemClass.Availability.Game) != ItemClass.Availability.None)
				{
					for (int l = 0; l < num2; l++)
					{
						TransportInfo prefab3 = PrefabCollection<TransportInfo>.GetPrefab((uint)l);
						if (prefab3 != null && prefab3.m_UnlockMilestone != null && prefab3.m_prefabDataIndex != -1)
						{
							PrefabCollection<TransportInfo>.Serialize((uint)prefab3.m_prefabDataIndex);
						}
					}
				}
			}
			finally
			{
				PrefabCollection<TransportInfo>.EndSerialize(s);
			}
			if ((instance.m_properties.m_mode & ItemClass.Availability.Game) != ItemClass.Availability.None)
			{
				for (int m = 0; m < num2; m++)
				{
					TransportInfo prefab4 = PrefabCollection<TransportInfo>.GetPrefab((uint)m);
					if (prefab4 != null && prefab4.m_UnlockMilestone != null && prefab4.m_prefabDataIndex != -1)
					{
						s.WriteObject<MilestoneInfo.Data>(prefab4.m_UnlockMilestone.GetData());
					}
				}
			}
			int num4 = 17;
			if (BuildConfig.SAVE_DATA_FORMAT_VERSION < 111006U)
			{
				num4 = 13;
			}
			for (int n = 0; n < num4; n++)
			{
				s.WriteUInt16((uint)instance2.m_lineNumber[n]);
			}
			for (int num5 = 1; num5 < num; num5++)
			{
				if (buffer[num5].m_flags != TransportLine.Flags.None)
				{
					s.WriteUInt16((uint)buffer[num5].m_lineNumber);
					s.WriteUInt16((uint)buffer[num5].m_stops);
					s.WriteUInt8((uint)buffer[num5].m_color.r);
					s.WriteUInt8((uint)buffer[num5].m_color.g);
					s.WriteUInt8((uint)buffer[num5].m_color.b);
					s.WriteUInt16((uint)buffer[num5].m_building);
					s.WriteUInt16((uint)buffer[num5].m_budget);
					s.WriteFloat(buffer[num5].m_totalLength);
					s.WriteUInt8((uint)buffer[num5].m_averageInterval);
					s.WriteUInt16((uint)buffer[num5].m_ticketPrice);
					buffer[num5].m_passengers.Serialize(s);
				}
			}
			for (int num6 = 0; num6 < num4; num6++)
			{
				instance2.m_passengers[num6].Serialize(s);
			}
			s.WriteObject<GenericGuide>(instance2.m_monorailLine);
			s.WriteObject<BuildingInstanceGuide>(instance2.m_endOfLineStation);
			s.WriteObject<BuildingInstanceGuide>(instance2.m_ferryBusHub);
			s.WriteObject<GenericGuide>(instance2.m_ferryLine);
			s.WriteObject<GenericGuide>(instance2.m_ferryDepot);
			s.WriteObject<GenericGuide>(instance2.m_blimpLine);
			s.WriteObject<GenericGuide>(instance2.m_blimpDepot);
			s.WriteInt16(instance2.m_transportLineVehicleSelection.Count);
			foreach (KeyValuePair<ushort, string> keyValuePair2 in instance2.m_transportLineVehicleSelection)
			{
				s.WriteInt16((int)keyValuePair2.Key);
				s.WriteSharedString(keyValuePair2.Value);
			}
			Singleton<LoadingManager>.instance.m_loadingProfilerSimulation.EndSerialize(s, "TransportManager");
		}

		// Token: 0x0600451E RID: 17694 RVA: 0x00322534 File Offset: 0x00320934
		public void Deserialize(DataSerializer s)
		{
			Singleton<LoadingManager>.instance.m_loadingProfilerSimulation.BeginDeserialize(s, "TransportManager");
			TransportManager instance = Singleton<TransportManager>.instance;
			TransportLine[] buffer = instance.m_lines.m_buffer;
			int num = buffer.Length;
			instance.m_lines.ClearUnused();
			if (s.version >= 52U)
			{
				EncodedArray.UInt @uint = EncodedArray.UInt.BeginRead(s);
				for (int i = 1; i < num; i++)
				{
					buffer[i].m_flags = (TransportLine.Flags)@uint.Read();
				}
				@uint.EndRead();
			}
			else
			{
				for (int j = 1; j < num; j++)
				{
					buffer[j].m_flags = TransportLine.Flags.None;
				}
			}
			uint num2 = 0U;
			if (s.version >= 151U)
			{
				num2 = s.ReadUInt16();
				this.m_milestoneInfoIndex = new uint[num2];
				this.m_UnlockMilestones = new MilestoneInfo.Data[num2];
			}
			if (s.version >= 52U)
			{
				PrefabCollection<TransportInfo>.BeginDeserialize(s);
				for (int k = 1; k < num; k++)
				{
					if (buffer[k].m_flags != TransportLine.Flags.None)
					{
						buffer[k].m_infoIndex = (ushort)PrefabCollection<TransportInfo>.Deserialize(true);
					}
				}
				for (uint num3 = 0U; num3 < num2; num3 += 1U)
				{
					this.m_milestoneInfoIndex[(int)((UIntPtr)num3)] = PrefabCollection<TransportInfo>.Deserialize(false);
				}
				PrefabCollection<TransportInfo>.EndDeserialize(s);
			}
			for (uint num4 = 0U; num4 < num2; num4 += 1U)
			{
				this.m_UnlockMilestones[(int)((UIntPtr)num4)] = s.ReadObject<MilestoneInfo.Data>();
			}
			int num5 = 17;
			if (s.version < 122U)
			{
				num5 = 0;
			}
			else if (s.version < 211U)
			{
				num5 = 5;
			}
			else if (s.version < 310U)
			{
				num5 = 8;
			}
			else if (s.version < 110000U)
			{
				num5 = 10;
			}
			else if (s.version < 110021U)
			{
				num5 = 12;
			}
			else if (s.version < 111006U)
			{
				num5 = 13;
			}
			else if (s.version < 113000U)
			{
				num5 = 14;
			}
			else if (s.version < 113008U)
			{
				num5 = 16;
			}
			int num6 = ((s.version < 153U) ? 0 : num5);
			for (int l = 0; l < num6; l++)
			{
				instance.m_lineNumber[l] = (ushort)s.ReadUInt16();
			}
			for (int m = num6; m < 17; m++)
			{
				instance.m_lineNumber[m] = 0;
			}
			for (int n = 1; n < num; n++)
			{
				buffer[n].m_vehicles = 0;
				if (buffer[n].m_flags != TransportLine.Flags.None)
				{
					if ((buffer[n].m_flags & TransportLine.Flags.Temporary) != TransportLine.Flags.None)
					{
						TransportLine[] array = buffer;
						int num7 = n;
						array[num7].m_flags = array[num7].m_flags | TransportLine.Flags.Hidden;
					}
					else
					{
						TransportLine[] array2 = buffer;
						int num8 = n;
						array2[num8].m_flags = array2[num8].m_flags & ~(TransportLine.Flags.Hidden | TransportLine.Flags.Selected);
					}
					TransportLine[] array3 = buffer;
					int num9 = n;
					array3[num9].m_flags = array3[num9].m_flags & ~TransportLine.Flags.Highlighted;
					if (s.version >= 153U)
					{
						buffer[n].m_lineNumber = (ushort)s.ReadUInt16();
					}
					else if ((buffer[n].m_flags & TransportLine.Flags.Temporary) != TransportLine.Flags.None)
					{
						buffer[n].m_lineNumber = 0;
					}
					else
					{
						TransportLine[] array4 = buffer;
						int num10 = n;
						ushort[] lineNumber = instance.m_lineNumber;
						ushort infoIndex = buffer[n].m_infoIndex;
						array4[num10].m_lineNumber = (lineNumber[(int)infoIndex] = lineNumber[(int)infoIndex] + 1);
					}
					buffer[n].m_stops = (ushort)s.ReadUInt16();
					if (s.version >= 121U)
					{
						buffer[n].m_color.r = (byte)s.ReadUInt8();
						buffer[n].m_color.g = (byte)s.ReadUInt8();
						buffer[n].m_color.b = (byte)s.ReadUInt8();
						buffer[n].m_color.a = byte.MaxValue;
					}
					else
					{
						buffer[n].m_color = default(Color32);
					}
					if (s.version >= 288U)
					{
						buffer[n].m_building = (ushort)s.ReadUInt16();
					}
					else
					{
						buffer[n].m_building = 0;
					}
					if (s.version >= 314U)
					{
						buffer[n].m_budget = (ushort)s.ReadUInt16();
						buffer[n].m_totalLength = s.ReadFloat();
					}
					else
					{
						buffer[n].m_budget = 100;
						buffer[n].m_totalLength = 0f;
					}
					if (s.version >= 320U)
					{
						buffer[n].m_averageInterval = (byte)s.ReadUInt8();
					}
					else
					{
						buffer[n].m_averageInterval = 0;
					}
					if (s.version >= 110023U)
					{
						buffer[n].m_ticketPrice = (ushort)s.ReadUInt16();
					}
					else
					{
						buffer[n].m_ticketPrice = 0;
					}
					if (s.version >= 122U)
					{
						buffer[n].m_passengers.Deserialize(s);
					}
					else
					{
						buffer[n].m_passengers = default(TransportPassengerData);
					}
					if (s.version >= 112004U && s.version < 112022U)
					{
						s.ReadUInt16();
					}
					if (s.version >= 112022U && s.version < 114001U)
					{
						string text = s.ReadUniqueString();
						instance.m_transportLineVehicleSelection[(ushort)n] = text;
					}
					if (s.version < 112022U)
					{
						instance.m_transportLineVehicleSelection[(ushort)n] = "#DEFAULT#";
					}
				}
				else
				{
					buffer[n].m_lineNumber = 0;
					buffer[n].m_stops = 0;
					buffer[n].m_building = 0;
					buffer[n].m_budget = 0;
					buffer[n].m_totalLength = 0f;
					buffer[n].m_averageInterval = 0;
					buffer[n].m_ticketPrice = 0;
					buffer[n].m_color = default(Color32);
					buffer[n].m_passengers = default(TransportPassengerData);
					instance.m_lines.ReleaseItem((ushort)n);
				}
			}
			for (int num11 = 0; num11 < num5; num11++)
			{
				instance.m_passengers[num11].Deserialize(s);
			}
			for (int num12 = num5; num12 < 17; num12++)
			{
				instance.m_passengers[num12] = default(TransportPassengerData);
			}
			if (s.version >= 316U)
			{
				instance.m_monorailLine = s.ReadObject<GenericGuide>();
				instance.m_endOfLineStation = s.ReadObject<BuildingInstanceGuide>();
			}
			else
			{
				instance.m_monorailLine = null;
				instance.m_endOfLineStation = null;
			}
			if (s.version >= 317U)
			{
				instance.m_ferryBusHub = s.ReadObject<BuildingInstanceGuide>();
			}
			else
			{
				instance.m_ferryBusHub = null;
			}
			if (s.version >= 318U)
			{
				instance.m_ferryLine = s.ReadObject<GenericGuide>();
				instance.m_ferryDepot = s.ReadObject<GenericGuide>();
				instance.m_blimpLine = s.ReadObject<GenericGuide>();
				instance.m_blimpDepot = s.ReadObject<GenericGuide>();
			}
			else
			{
				instance.m_ferryLine = null;
				instance.m_ferryDepot = null;
				instance.m_blimpLine = null;
				instance.m_blimpDepot = null;
			}
			if (s.version >= 114001U)
			{
				int num13 = s.ReadInt16();
				for (int num14 = 0; num14 < num13; num14++)
				{
					ushort num15 = (ushort)s.ReadInt16();
					string text2 = s.ReadSharedString();
					instance.m_transportLineVehicleSelection[num15] = text2;
				}
			}
			Singleton<LoadingManager>.instance.m_loadingProfilerSimulation.EndDeserialize(s, "TransportManager");
		}

		// Token: 0x0600451F RID: 17695 RVA: 0x00322D84 File Offset: 0x00321184
		public void AfterDeserialize(DataSerializer s)
		{
			Singleton<LoadingManager>.instance.m_loadingProfilerSimulation.BeginAfterDeserialize(s, "TransportManager");
			Singleton<LoadingManager>.instance.WaitUntilEssentialScenesLoaded();
			PrefabCollection<TransportInfo>.BindPrefabs();
			TransportManager instance = Singleton<TransportManager>.instance;
			TransportLine[] buffer = instance.m_lines.m_buffer;
			int num = buffer.Length;
			for (int i = 1; i < num; i++)
			{
				if (buffer[i].m_flags != TransportLine.Flags.None)
				{
					TransportInfo info = buffer[i].Info;
					int num2 = -1;
					if (info != null)
					{
						buffer[i].m_infoIndex = (ushort)info.m_prefabDataIndex;
						num2 = info.m_netInfo.m_prefabDataIndex;
						if (s.version < 110023U)
						{
							buffer[i].m_ticketPrice = (ushort)info.m_ticketPrice;
						}
					}
					ushort stops = buffer[i].m_stops;
					ushort num3 = stops;
					int num4 = 0;
					while (num3 != 0)
					{
						if (Singleton<NetManager>.instance.m_nodes.m_buffer[(int)num3].m_transportLine == (ushort)i)
						{
							CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid transport line loop!\n" + Environment.StackTrace);
							TransportLine[] array = buffer;
							int num5 = i;
							array[num5].m_flags = array[num5].m_flags | TransportLine.Flags.Invalid;
							break;
						}
						if (Singleton<NetManager>.instance.m_nodes.m_buffer[(int)num3].m_flags == NetNode.Flags.None || Singleton<NetManager>.instance.m_nodes.m_buffer[(int)num3].m_transportLine != 0)
						{
							CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid transport line node!\n" + Environment.StackTrace);
							TransportLine[] array2 = buffer;
							int num6 = i;
							array2[num6].m_flags = array2[num6].m_flags | TransportLine.Flags.Invalid;
							break;
						}
						Singleton<NetManager>.instance.m_nodes.m_buffer[(int)num3].m_transportLine = (ushort)i;
						if (num2 != -1)
						{
							Singleton<NetManager>.instance.m_nodes.m_buffer[(int)num3].m_infoIndex = (ushort)num2;
						}
						ushort nextSegment = TransportLine.GetNextSegment(num3);
						if (nextSegment != 0)
						{
							if (num2 != -1)
							{
								Singleton<NetManager>.instance.m_segments.m_buffer[(int)nextSegment].m_infoIndex = (ushort)num2;
							}
							num3 = Singleton<NetManager>.instance.m_segments.m_buffer[(int)nextSegment].m_endNode;
						}
						else
						{
							num3 = 0;
						}
						if (num3 == stops)
						{
							break;
						}
						if (++num4 >= 32768)
						{
							CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
							break;
						}
					}
				}
			}
			if (this.m_milestoneInfoIndex != null)
			{
				int num7 = this.m_milestoneInfoIndex.Length;
				for (int j = 0; j < num7; j++)
				{
					TransportInfo prefab = PrefabCollection<TransportInfo>.GetPrefab(this.m_milestoneInfoIndex[j]);
					if (prefab != null)
					{
						MilestoneInfo unlockMilestone = prefab.m_UnlockMilestone;
						if (unlockMilestone != null)
						{
							unlockMilestone.SetData(this.m_UnlockMilestones[j]);
						}
					}
				}
			}
			instance.m_linesUpdated = true;
			int num8 = instance.m_updatedLines.Length;
			for (int k = 0; k < num8; k++)
			{
				instance.m_updatedLines[k] = ulong.MaxValue;
			}
			int num9 = instance.m_patches.Length;
			for (int l = 0; l < num9; l++)
			{
				TransportPatch transportPatch = instance.m_patches[l];
				int num10 = 0;
				while (transportPatch != null)
				{
					transportPatch.m_isDirty = true;
					transportPatch = transportPatch.m_nextPatch;
					if (++num10 >= 100)
					{
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}
			}
			instance.m_patchesDirty = true;
			instance.m_lineCount = (int)(instance.m_lines.ItemCount() - 1U);
			Singleton<LoadingManager>.instance.m_loadingProfilerSimulation.EndAfterDeserialize(s, "TransportManager");
		}

		// Token: 0x04004DD6 RID: 19926
		private uint[] m_milestoneInfoIndex;

		// Token: 0x04004DD7 RID: 19927
		private MilestoneInfo.Data[] m_UnlockMilestones;
	}
}
