using System;
using System.Collections;
using System.Collections.Generic;
using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.IO;
using ColossalFramework.Math;
using UnityEngine;

// Token: 0x020006A2 RID: 1698
public class VehicleManager : SimulationManagerBase<VehicleManager, VehicleProperties>, ISimulationManager, IRenderableManager, IAudibleManager
{
	// Token: 0x17000526 RID: 1318
	// (get) Token: 0x06004F11 RID: 20241 RVA: 0x0039C43F File Offset: 0x0039A83F
	public uint cityVehicleCount
	{
		get
		{
			return this.m_finalVehicleCount >> 4;
		}
	}

	// Token: 0x06004F12 RID: 20242 RVA: 0x0039C44C File Offset: 0x0039A84C
	private static int GetTransferIndex(ItemClass.Service service, ItemClass.SubService subService, ItemClass.Level level)
	{
		int num;
		if (subService != ItemClass.SubService.None)
		{
			num = 28 + subService - ItemClass.SubService.ResidentialLow;
		}
		else
		{
			num = service - ItemClass.Service.Residential;
		}
		return (int)(num * 5 + level);
	}

	// Token: 0x06004F13 RID: 20243 RVA: 0x0039C478 File Offset: 0x0039A878
	protected override void Awake()
	{
		base.Awake();
		this.m_vehicles = new Array16<Vehicle>(16384U);
		this.m_parkedVehicles = new Array16<VehicleParked>(32768U);
		this.m_updatedParked = new ulong[512];
		this.m_renderBuffer = new ulong[256];
		this.m_renderBuffer2 = new ulong[512];
		this.m_vehicleGrid = new ushort[291600];
		this.m_vehicleGrid2 = new ushort[2916];
		this.m_parkedGrid = new ushort[291600];
		this.m_transferVehicles = new FastList<ushort>[350];
		this.m_materialBlock = new MaterialPropertyBlock();
		this.ID_TyreMatrix = Shader.PropertyToID("_TyreMatrix");
		this.ID_TyrePosition = Shader.PropertyToID("_TyrePosition");
		this.ID_LightState = Shader.PropertyToID("_LightState");
		this.ID_Color = Shader.PropertyToID("_Color");
		this.ID_ColorV0 = Shader.PropertyToID("_ColorV0");
		this.ID_ColorV1 = Shader.PropertyToID("_ColorV1");
		this.ID_ColorV2 = Shader.PropertyToID("_ColorV2");
		this.ID_ColorV3 = Shader.PropertyToID("_ColorV3");
		this.ID_MainTex = Shader.PropertyToID("_MainTex");
		this.ID_XYSMap = Shader.PropertyToID("_XYSMap");
		this.ID_ACIMap = Shader.PropertyToID("_ACIMap");
		this.ID_CCCMap = Shader.PropertyToID("_CCCMap");
		this.ID_AtlasRect = Shader.PropertyToID("_AtlasRect");
		this.ID_VehicleTransform = Shader.PropertyToID("_VehicleTransform");
		this.ID_VehicleLightState = Shader.PropertyToID("_VehicleLightState");
		this.ID_VehicleColor = Shader.PropertyToID("_VehicleColor");
		this.ID_TyreLocation = Shader.PropertyToID("_TyreLocation");
		this.ID_RacerRect = Shader.PropertyToID("_RacerRect");
		this.ID_RacerData = Shader.PropertyToID("_RacerData");
		this.m_audioGroup = new AudioGroup(5, new SavedFloat(Settings.effectAudioVolume, Settings.gameSettingsFile, DefaultSettings.effectAudioVolume, true));
		this.m_undergroundLayer = LayerMask.NameToLayer("MetroTunnels");
		ushort num;
		this.m_vehicles.CreateItem(out num);
		this.m_parkedVehicles.CreateItem(out num);
		this.m_busTypeWeights = new int[2];
	}

	// Token: 0x06004F14 RID: 20244 RVA: 0x0039C6B0 File Offset: 0x0039AAB0
	private void OnDestroy()
	{
		if (this.m_lodRgbAtlas != null)
		{
			Object.Destroy(this.m_lodRgbAtlas);
			this.m_lodRgbAtlas = null;
		}
		if (this.m_lodXysAtlas != null)
		{
			Object.Destroy(this.m_lodXysAtlas);
			this.m_lodXysAtlas = null;
		}
		if (this.m_lodAciAtlas != null)
		{
			Object.Destroy(this.m_lodAciAtlas);
			this.m_lodAciAtlas = null;
		}
	}

	// Token: 0x06004F15 RID: 20245 RVA: 0x0039C728 File Offset: 0x0039AB28
	public override void InitializeProperties(VehicleProperties properties)
	{
		base.InitializeProperties(properties);
		int num = PrefabCollection<VehicleInfo>.LoadedCount();
		for (int i = 0; i < num; i++)
		{
			VehicleInfo loaded = PrefabCollection<VehicleInfo>.GetLoaded((uint)i);
			if (!(loaded == null))
			{
				if (loaded.m_vehicleType == VehicleInfo.VehicleType.Car && loaded.m_class.m_service == ItemClass.Service.PublicTransport && loaded.m_class.m_subService == ItemClass.SubService.PublicTransportBus)
				{
					if (loaded.m_class.m_level == ItemClass.Level.Level1)
					{
						this.m_busTypeWeights[0]++;
					}
					else if (loaded.m_class.m_level == ItemClass.Level.Level2)
					{
						this.m_busTypeWeights[1]++;
					}
				}
			}
		}
	}

	// Token: 0x06004F16 RID: 20246 RVA: 0x0039C7E4 File Offset: 0x0039ABE4
	public override void DestroyProperties(VehicleProperties properties)
	{
		if (this.m_properties == properties)
		{
			if (this.m_audioGroup != null)
			{
				this.m_audioGroup.Reset();
			}
			if (this.m_lodRgbAtlas != null)
			{
				Object.Destroy(this.m_lodRgbAtlas);
				this.m_lodRgbAtlas = null;
			}
			if (this.m_lodXysAtlas != null)
			{
				Object.Destroy(this.m_lodXysAtlas);
				this.m_lodXysAtlas = null;
			}
			if (this.m_lodAciAtlas != null)
			{
				Object.Destroy(this.m_lodAciAtlas);
				this.m_lodAciAtlas = null;
			}
		}
		base.DestroyProperties(properties);
	}

	// Token: 0x06004F17 RID: 20247 RVA: 0x0039C888 File Offset: 0x0039AC88
	public override void CheckReferences()
	{
		base.CheckReferences();
		Singleton<LoadingManager>.instance.QueueLoadingAction(this.CheckReferencesImpl());
	}

	// Token: 0x06004F18 RID: 20248 RVA: 0x0039C8A0 File Offset: 0x0039ACA0
	private IEnumerator CheckReferencesImpl()
	{
		Singleton<LoadingManager>.instance.m_loadingProfilerMain.BeginLoading("VehicleManager.CheckReferences");
		int vehicleCount = PrefabCollection<VehicleInfo>.LoadedCount();
		for (int i = 0; i < vehicleCount; i++)
		{
			VehicleInfo loaded = PrefabCollection<VehicleInfo>.GetLoaded((uint)i);
			if (loaded != null)
			{
				try
				{
					loaded.CheckReferences();
				}
				catch (PrefabException ex)
				{
					CODebugBase<LogChannel>.Error(LogChannel.Core, string.Concat(new string[]
					{
						ex.m_prefabInfo.gameObject.name,
						": ",
						ex.Message,
						"\n",
						ex.StackTrace
					}), ex.m_prefabInfo.gameObject);
					LoadingManager instance = Singleton<LoadingManager>.instance;
					string brokenAssets = instance.m_brokenAssets;
					instance.m_brokenAssets = string.Concat(new string[]
					{
						brokenAssets,
						"\n",
						ex.m_prefabInfo.gameObject.name,
						": ",
						ex.Message
					});
				}
			}
		}
		Singleton<LoadingManager>.instance.m_loadingProfilerMain.EndLoading();
		yield return 0;
		yield break;
	}

	// Token: 0x06004F19 RID: 20249 RVA: 0x0039C8B4 File Offset: 0x0039ACB4
	public override void InitRenderData()
	{
		base.InitRenderData();
		Singleton<LoadingManager>.instance.QueueLoadingAction(this.InitRenderDataImpl());
	}

	// Token: 0x06004F1A RID: 20250 RVA: 0x0039C8CC File Offset: 0x0039ACCC
	private IEnumerator InitRenderDataImpl()
	{
		Singleton<LoadingManager>.instance.m_loadingProfilerMain.BeginLoading("VehicleManager.InitRenderData");
		FastList<VehicleInfoBase> infos = new FastList<VehicleInfoBase>();
		FastList<Texture2D> rgbTextures = new FastList<Texture2D>();
		FastList<Texture2D> xysTextures = new FastList<Texture2D>();
		FastList<Texture2D> aciTextures = new FastList<Texture2D>();
		int vehicleCount = PrefabCollection<VehicleInfo>.LoadedCount();
		infos.EnsureCapacity(vehicleCount * 2);
		rgbTextures.EnsureCapacity(vehicleCount * 2);
		xysTextures.EnsureCapacity(vehicleCount * 2);
		aciTextures.EnsureCapacity(vehicleCount * 2);
		for (int i = 0; i < vehicleCount; i++)
		{
			VehicleInfo loaded = PrefabCollection<VehicleInfo>.GetLoaded((uint)i);
			if (loaded != null)
			{
				if (!loaded.m_hasLodData)
				{
					try
					{
						loaded.m_hasLodData = true;
						if (loaded.m_lodMesh == null || loaded.m_lodMaterial == null)
						{
							loaded.InitMeshData(new Rect(0f, 0f, 1f, 1f), null, null, null);
						}
						else
						{
							Texture2D texture2D = null;
							if (loaded.m_lodMaterial.HasProperty(this.ID_MainTex))
							{
								texture2D = loaded.m_lodMaterial.GetTexture(this.ID_MainTex) as Texture2D;
							}
							Texture2D texture2D2 = null;
							if (loaded.m_lodMaterial.HasProperty(this.ID_XYSMap))
							{
								texture2D2 = loaded.m_lodMaterial.GetTexture(this.ID_XYSMap) as Texture2D;
							}
							Texture2D texture2D3 = null;
							if (loaded.m_lodMaterial.HasProperty(this.ID_ACIMap))
							{
								texture2D3 = loaded.m_lodMaterial.GetTexture(this.ID_ACIMap) as Texture2D;
							}
							if (texture2D == null && texture2D2 == null && texture2D3 == null && loaded.m_material.mainTexture == null)
							{
								loaded.InitMeshData(new Rect(0f, 0f, 1f, 1f), null, null, null);
							}
							else
							{
								if (texture2D == null)
								{
									throw new PrefabException(loaded, "LOD diffuse null");
								}
								if (texture2D2 == null)
								{
									throw new PrefabException(loaded, "LOD xys null");
								}
								if (texture2D3 == null)
								{
									throw new PrefabException(loaded, "LOD aci null");
								}
								if (texture2D2.width != texture2D.width || texture2D2.height != texture2D.height)
								{
									throw new PrefabException(loaded, "LOD xys size doesnt match diffuse size");
								}
								if (texture2D3.width != texture2D.width || texture2D3.height != texture2D.height)
								{
									throw new PrefabException(loaded, "LOD aci size doesnt match diffuse size");
								}
								try
								{
									texture2D.GetPixel(0, 0);
								}
								catch (UnityException)
								{
									throw new PrefabException(loaded, "LOD diffuse not readable");
								}
								try
								{
									texture2D2.GetPixel(0, 0);
								}
								catch (UnityException)
								{
									throw new PrefabException(loaded, "LOD xys not readable");
								}
								try
								{
									texture2D3.GetPixel(0, 0);
								}
								catch (UnityException)
								{
									throw new PrefabException(loaded, "LOD aci not readable");
								}
								infos.Add(loaded);
								rgbTextures.Add(texture2D);
								xysTextures.Add(texture2D2);
								aciTextures.Add(texture2D3);
							}
						}
					}
					catch (PrefabException ex)
					{
						CODebugBase<LogChannel>.Error(LogChannel.Core, string.Concat(new string[]
						{
							ex.m_prefabInfo.gameObject.name,
							": ",
							ex.Message,
							"\n",
							ex.StackTrace
						}), ex.m_prefabInfo.gameObject);
						LoadingManager instance = Singleton<LoadingManager>.instance;
						string text = instance.m_brokenAssets;
						instance.m_brokenAssets = string.Concat(new string[]
						{
							text,
							"\n",
							ex.m_prefabInfo.gameObject.name,
							": ",
							ex.Message
						});
					}
				}
				if (loaded.m_subMeshes != null)
				{
					for (int j = 0; j < loaded.m_subMeshes.Length; j++)
					{
						try
						{
							VehicleInfoBase subInfo = loaded.m_subMeshes[j].m_subInfo;
							if (subInfo != null && !subInfo.m_hasLodData)
							{
								subInfo.m_hasLodData = true;
								if (subInfo.m_lodMesh == null || subInfo.m_lodMaterial == null)
								{
									subInfo.InitMeshData(new Rect(0f, 0f, 1f, 1f), null, null, null);
								}
								else
								{
									Texture2D texture2D4 = null;
									if (subInfo.m_lodMaterial.HasProperty(this.ID_MainTex))
									{
										texture2D4 = subInfo.m_lodMaterial.GetTexture(this.ID_MainTex) as Texture2D;
									}
									Texture2D texture2D5 = null;
									if (subInfo.m_lodMaterial.HasProperty(this.ID_XYSMap))
									{
										texture2D5 = subInfo.m_lodMaterial.GetTexture(this.ID_XYSMap) as Texture2D;
									}
									Texture2D texture2D6 = null;
									if (subInfo.m_lodMaterial.HasProperty(this.ID_ACIMap))
									{
										texture2D6 = subInfo.m_lodMaterial.GetTexture(this.ID_ACIMap) as Texture2D;
									}
									if (texture2D4 == null && texture2D5 == null && texture2D6 == null && subInfo.m_material.mainTexture == null)
									{
										subInfo.InitMeshData(new Rect(0f, 0f, 1f, 1f), null, null, null);
									}
									else
									{
										if (texture2D4 == null)
										{
											throw new PrefabException(subInfo, "LOD diffuse null");
										}
										if (texture2D5 == null)
										{
											throw new PrefabException(subInfo, "LOD xys null");
										}
										if (texture2D6 == null)
										{
											throw new PrefabException(subInfo, "LOD aci null");
										}
										if (texture2D5.width != texture2D4.width || texture2D5.height != texture2D4.height)
										{
											throw new PrefabException(subInfo, "LOD xys size not match diffuse size");
										}
										if (texture2D6.width != texture2D4.width || texture2D6.height != texture2D4.height)
										{
											throw new PrefabException(subInfo, "LOD aci size not match diffuse size");
										}
										try
										{
											texture2D4.GetPixel(0, 0);
										}
										catch (UnityException)
										{
											throw new PrefabException(subInfo, "LOD diffuse not readable");
										}
										try
										{
											texture2D5.GetPixel(0, 0);
										}
										catch (UnityException)
										{
											throw new PrefabException(subInfo, "LOD xys not readable");
										}
										try
										{
											texture2D6.GetPixel(0, 0);
										}
										catch (UnityException)
										{
											throw new PrefabException(subInfo, "LOD aci not readable");
										}
										infos.Add(subInfo);
										rgbTextures.Add(texture2D4);
										xysTextures.Add(texture2D5);
										aciTextures.Add(texture2D6);
									}
								}
							}
						}
						catch (PrefabException ex2)
						{
							CODebugBase<LogChannel>.Error(LogChannel.Core, string.Concat(new string[]
							{
								ex2.m_prefabInfo.gameObject.name,
								": ",
								ex2.Message,
								"\n",
								ex2.StackTrace
							}), ex2.m_prefabInfo.gameObject);
							LoadingManager instance2 = Singleton<LoadingManager>.instance;
							string text = instance2.m_brokenAssets;
							instance2.m_brokenAssets = string.Concat(new string[]
							{
								text,
								"\n",
								ex2.m_prefabInfo.gameObject.name,
								": ",
								ex2.Message
							});
						}
					}
				}
			}
		}
		if (this.m_lodRgbAtlas == null)
		{
			this.m_lodRgbAtlas = new Texture2D(1024, 1024, 10, true, false);
			this.m_lodRgbAtlas.filterMode = 2;
			this.m_lodRgbAtlas.anisoLevel = 4;
		}
		if (this.m_lodXysAtlas == null)
		{
			this.m_lodXysAtlas = new Texture2D(1024, 1024, 10, true, true);
			this.m_lodXysAtlas.filterMode = 2;
			this.m_lodXysAtlas.anisoLevel = 4;
		}
		if (this.m_lodAciAtlas == null)
		{
			this.m_lodAciAtlas = new Texture2D(1024, 1024, 10, true, true);
			this.m_lodAciAtlas.filterMode = 2;
			this.m_lodAciAtlas.anisoLevel = 4;
		}
		Singleton<LoadingManager>.instance.m_loadingProfilerMain.PauseLoading();
		yield return 0;
		Singleton<LoadingManager>.instance.m_loadingProfilerMain.ContinueLoading();
		Rect[] rect = this.m_lodRgbAtlas.PackTextures(rgbTextures.ToArray(), 0, 4096, false);
		Singleton<LoadingManager>.instance.m_loadingProfilerMain.PauseLoading();
		yield return 0;
		Singleton<LoadingManager>.instance.m_loadingProfilerMain.ContinueLoading();
		this.m_lodXysAtlas.PackTextures(xysTextures.ToArray(), 0, 4096, false);
		Singleton<LoadingManager>.instance.m_loadingProfilerMain.PauseLoading();
		yield return 0;
		Singleton<LoadingManager>.instance.m_loadingProfilerMain.ContinueLoading();
		this.m_lodAciAtlas.PackTextures(aciTextures.ToArray(), 0, 4096, false);
		Singleton<LoadingManager>.instance.m_loadingProfilerMain.PauseLoading();
		yield return 0;
		Singleton<LoadingManager>.instance.m_loadingProfilerMain.ContinueLoading();
		for (int k = 0; k < infos.m_size; k++)
		{
			try
			{
				infos.m_buffer[k].InitMeshData(rect[k], this.m_lodRgbAtlas, this.m_lodXysAtlas, this.m_lodAciAtlas);
			}
			catch (PrefabException ex3)
			{
				CODebugBase<LogChannel>.Error(LogChannel.Core, string.Concat(new string[]
				{
					ex3.m_prefabInfo.gameObject.name,
					": ",
					ex3.Message,
					"\n",
					ex3.StackTrace
				}), ex3.m_prefabInfo.gameObject);
				LoadingManager instance3 = Singleton<LoadingManager>.instance;
				string text = instance3.m_brokenAssets;
				instance3.m_brokenAssets = string.Concat(new string[]
				{
					text,
					"\n",
					ex3.m_prefabInfo.gameObject.name,
					": ",
					ex3.Message
				});
			}
		}
		Singleton<LoadingManager>.instance.m_loadingProfilerMain.EndLoading();
		yield return 0;
		yield break;
	}

	// Token: 0x06004F1B RID: 20251 RVA: 0x0039C8E8 File Offset: 0x0039ACE8
	protected override void EndRenderingImpl(RenderManager.CameraInfo cameraInfo)
	{
		float levelOfDetailFactor = RenderManager.LevelOfDetailFactor;
		float near = cameraInfo.m_near;
		float num = Mathf.Min(levelOfDetailFactor * 5000f, Mathf.Min(levelOfDetailFactor * 2000f + cameraInfo.m_height * 0.6f, cameraInfo.m_far));
		Vector3 vector = cameraInfo.m_position + cameraInfo.m_directionA * near;
		Vector3 vector2 = cameraInfo.m_position + cameraInfo.m_directionB * near;
		Vector3 vector3 = cameraInfo.m_position + cameraInfo.m_directionC * near;
		Vector3 vector4 = cameraInfo.m_position + cameraInfo.m_directionD * near;
		Vector3 vector5 = cameraInfo.m_position + cameraInfo.m_directionA * num;
		Vector3 vector6 = cameraInfo.m_position + cameraInfo.m_directionB * num;
		Vector3 vector7 = cameraInfo.m_position + cameraInfo.m_directionC * num;
		Vector3 vector8 = cameraInfo.m_position + cameraInfo.m_directionD * num;
		Vector3 vector9 = Vector3.Min(Vector3.Min(Vector3.Min(vector, vector2), Vector3.Min(vector3, vector4)), Vector3.Min(Vector3.Min(vector5, vector6), Vector3.Min(vector7, vector8)));
		Vector3 vector10 = Vector3.Max(Vector3.Max(Vector3.Max(vector, vector2), Vector3.Max(vector3, vector4)), Vector3.Max(Vector3.Max(vector5, vector6), Vector3.Max(vector7, vector8)));
		int num2 = Mathf.Max((int)((vector9.x - 10f) / 32f + 270f), 0);
		int num3 = Mathf.Max((int)((vector9.z - 10f) / 32f + 270f), 0);
		int num4 = Mathf.Min((int)((vector10.x + 10f) / 32f + 270f), 539);
		int num5 = Mathf.Min((int)((vector10.z + 10f) / 32f + 270f), 539);
		for (int i = num3; i <= num5; i++)
		{
			for (int j = num2; j <= num4; j++)
			{
				ushort num6 = this.m_vehicleGrid[i * 540 + j];
				if (num6 != 0)
				{
					this.m_renderBuffer[num6 >> 6] |= 1UL << (int)num6;
				}
			}
		}
		float near2 = cameraInfo.m_near;
		float num7 = Mathf.Min(2000f, cameraInfo.m_far);
		Vector3 vector11 = cameraInfo.m_position + cameraInfo.m_directionA * near2;
		Vector3 vector12 = cameraInfo.m_position + cameraInfo.m_directionB * near2;
		Vector3 vector13 = cameraInfo.m_position + cameraInfo.m_directionC * near2;
		Vector3 vector14 = cameraInfo.m_position + cameraInfo.m_directionD * near2;
		Vector3 vector15 = cameraInfo.m_position + cameraInfo.m_directionA * num7;
		Vector3 vector16 = cameraInfo.m_position + cameraInfo.m_directionB * num7;
		Vector3 vector17 = cameraInfo.m_position + cameraInfo.m_directionC * num7;
		Vector3 vector18 = cameraInfo.m_position + cameraInfo.m_directionD * num7;
		Vector3 vector19 = Vector3.Min(Vector3.Min(Vector3.Min(vector11, vector12), Vector3.Min(vector13, vector14)), Vector3.Min(Vector3.Min(vector15, vector16), Vector3.Min(vector17, vector18)));
		Vector3 vector20 = Vector3.Max(Vector3.Max(Vector3.Max(vector11, vector12), Vector3.Max(vector13, vector14)), Vector3.Max(Vector3.Max(vector15, vector16), Vector3.Max(vector17, vector18)));
		int num8 = Mathf.Max((int)((vector19.x - 10f) / 32f + 270f), 0);
		int num9 = Mathf.Max((int)((vector19.z - 10f) / 32f + 270f), 0);
		int num10 = Mathf.Min((int)((vector20.x + 10f) / 32f + 270f), 539);
		int num11 = Mathf.Min((int)((vector20.z + 10f) / 32f + 270f), 539);
		for (int k = num9; k <= num11; k++)
		{
			for (int l = num8; l <= num10; l++)
			{
				ushort num12 = this.m_parkedGrid[k * 540 + l];
				if (num12 != 0)
				{
					this.m_renderBuffer2[num12 >> 6] |= 1UL << (int)num12;
				}
			}
		}
		float near3 = cameraInfo.m_near;
		float num13 = Mathf.Min(10000f, cameraInfo.m_far);
		Vector3 vector21 = cameraInfo.m_position + cameraInfo.m_directionA * near3;
		Vector3 vector22 = cameraInfo.m_position + cameraInfo.m_directionB * near3;
		Vector3 vector23 = cameraInfo.m_position + cameraInfo.m_directionC * near3;
		Vector3 vector24 = cameraInfo.m_position + cameraInfo.m_directionD * near3;
		Vector3 vector25 = cameraInfo.m_position + cameraInfo.m_directionA * num13;
		Vector3 vector26 = cameraInfo.m_position + cameraInfo.m_directionB * num13;
		Vector3 vector27 = cameraInfo.m_position + cameraInfo.m_directionC * num13;
		Vector3 vector28 = cameraInfo.m_position + cameraInfo.m_directionD * num13;
		Vector3 vector29 = Vector3.Min(Vector3.Min(Vector3.Min(vector21, vector22), Vector3.Min(vector23, vector24)), Vector3.Min(Vector3.Min(vector25, vector26), Vector3.Min(vector27, vector28)));
		Vector3 vector30 = Vector3.Max(Vector3.Max(Vector3.Max(vector21, vector22), Vector3.Max(vector23, vector24)), Vector3.Max(Vector3.Max(vector25, vector26), Vector3.Max(vector27, vector28)));
		if (cameraInfo.m_shadowOffset.x < 0f)
		{
			vector30.x = Mathf.Min(cameraInfo.m_position.x + num13, vector30.x - cameraInfo.m_shadowOffset.x);
		}
		else
		{
			vector29.x = Mathf.Max(cameraInfo.m_position.x - num13, vector29.x - cameraInfo.m_shadowOffset.x);
		}
		if (cameraInfo.m_shadowOffset.z < 0f)
		{
			vector30.z = Mathf.Min(cameraInfo.m_position.z + num13, vector30.z - cameraInfo.m_shadowOffset.z);
		}
		else
		{
			vector29.z = Mathf.Max(cameraInfo.m_position.z - num13, vector29.z - cameraInfo.m_shadowOffset.z);
		}
		int num14 = Mathf.Max((int)((vector29.x - 50f) / 320f + 27f), 0);
		int num15 = Mathf.Max((int)((vector29.z - 50f) / 320f + 27f), 0);
		int num16 = Mathf.Min((int)((vector30.x + 50f) / 320f + 27f), 53);
		int num17 = Mathf.Min((int)((vector30.z + 50f) / 320f + 27f), 53);
		for (int m = num15; m <= num17; m++)
		{
			for (int n = num14; n <= num16; n++)
			{
				ushort num18 = this.m_vehicleGrid2[m * 54 + n];
				if (num18 != 0)
				{
					this.m_renderBuffer[num18 >> 6] |= 1UL << (int)num18;
				}
			}
		}
		int num19 = this.m_renderBuffer.Length;
		for (int num20 = 0; num20 < num19; num20++)
		{
			ulong num21 = this.m_renderBuffer[num20];
			if (num21 != 0UL)
			{
				for (int num22 = 0; num22 < 64; num22++)
				{
					ulong num23 = 1UL << num22;
					if ((num21 & num23) != 0UL)
					{
						ushort num24 = (ushort)((num20 << 6) | num22);
						if (!this.m_vehicles.m_buffer[(int)num24].RenderInstance(cameraInfo, num24))
						{
							num21 &= ~num23;
						}
						ushort num25 = this.m_vehicles.m_buffer[(int)num24].m_nextGridVehicle;
						int num26 = 0;
						while (num25 != 0)
						{
							int num27 = num25 >> 6;
							num23 = 1UL << (int)num25;
							if (num27 == num20)
							{
								if ((num21 & num23) != 0UL)
								{
									break;
								}
								num21 |= num23;
							}
							else
							{
								ulong num28 = this.m_renderBuffer[num27];
								if ((num28 & num23) != 0UL)
								{
									break;
								}
								this.m_renderBuffer[num27] = num28 | num23;
							}
							if (num25 > num24)
							{
								break;
							}
							num25 = this.m_vehicles.m_buffer[(int)num25].m_nextGridVehicle;
							if (++num26 > 16384)
							{
								CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
								break;
							}
						}
					}
				}
				this.m_renderBuffer[num20] = num21;
			}
		}
		int num29 = this.m_renderBuffer2.Length;
		for (int num30 = 0; num30 < num29; num30++)
		{
			ulong num31 = this.m_renderBuffer2[num30];
			if (num31 != 0UL)
			{
				for (int num32 = 0; num32 < 64; num32++)
				{
					ulong num33 = 1UL << num32;
					if ((num31 & num33) != 0UL)
					{
						ushort num34 = (ushort)((num30 << 6) | num32);
						if (!this.m_parkedVehicles.m_buffer[(int)num34].RenderInstance(cameraInfo, num34))
						{
							num31 &= ~num33;
						}
						ushort num35 = this.m_parkedVehicles.m_buffer[(int)num34].m_nextGridParked;
						int num36 = 0;
						while (num35 != 0)
						{
							int num37 = num35 >> 6;
							num33 = 1UL << (int)num35;
							if (num37 == num30)
							{
								if ((num31 & num33) != 0UL)
								{
									break;
								}
								num31 |= num33;
							}
							else
							{
								ulong num38 = this.m_renderBuffer2[num37];
								if ((num38 & num33) != 0UL)
								{
									break;
								}
								this.m_renderBuffer2[num37] = num38 | num33;
							}
							if (num35 > num34)
							{
								break;
							}
							num35 = this.m_parkedVehicles.m_buffer[(int)num35].m_nextGridParked;
							if (++num36 > 32768)
							{
								CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
								break;
							}
						}
					}
				}
				this.m_renderBuffer2[num30] = num31;
			}
		}
		int num39 = PrefabCollection<VehicleInfo>.PrefabCount();
		for (int num40 = 0; num40 < num39; num40++)
		{
			VehicleInfo prefab = PrefabCollection<VehicleInfo>.GetPrefab((uint)num40);
			if (prefab != null)
			{
				if (prefab.m_lodCount != 0)
				{
					Vehicle.RenderLod(cameraInfo, prefab);
				}
				if (prefab.m_undergroundLodCount != 0)
				{
					Vehicle.RenderUndergroundLod(cameraInfo, prefab);
				}
				if (prefab.m_subMeshes != null)
				{
					for (int num41 = 0; num41 < prefab.m_subMeshes.Length; num41++)
					{
						VehicleInfoBase subInfo = prefab.m_subMeshes[num41].m_subInfo;
						if (subInfo != null)
						{
							if (subInfo.m_lodCount != 0)
							{
								Vehicle.RenderLod(cameraInfo, subInfo);
							}
							if (subInfo.m_undergroundLodCount != 0)
							{
								Vehicle.RenderUndergroundLod(cameraInfo, subInfo);
							}
						}
					}
				}
			}
		}
	}

	// Token: 0x06004F1C RID: 20252 RVA: 0x0039D490 File Offset: 0x0039B890
	protected override void PlayAudioImpl(AudioManager.ListenerInfo listenerInfo)
	{
		if (this.m_properties != null)
		{
			LoadingManager instance = Singleton<LoadingManager>.instance;
			SimulationManager instance2 = Singleton<SimulationManager>.instance;
			AudioManager instance3 = Singleton<AudioManager>.instance;
			if (!instance.m_currentlyLoading)
			{
				int num = Mathf.Max((int)((listenerInfo.m_position.x - 150f) / 32f + 270f), 0);
				int num2 = Mathf.Max((int)((listenerInfo.m_position.z - 150f) / 32f + 270f), 0);
				int num3 = Mathf.Min((int)((listenerInfo.m_position.x + 150f) / 32f + 270f), 539);
				int num4 = Mathf.Min((int)((listenerInfo.m_position.z + 150f) / 32f + 270f), 539);
				for (int i = num2; i <= num4; i++)
				{
					for (int j = num; j <= num3; j++)
					{
						int num5 = i * 540 + j;
						ushort num6 = this.m_vehicleGrid[num5];
						int num7 = 0;
						while (num6 != 0)
						{
							this.m_vehicles.m_buffer[(int)num6].PlayAudio(listenerInfo, num6);
							num6 = this.m_vehicles.m_buffer[(int)num6].m_nextGridVehicle;
							if (++num7 >= 16384)
							{
								CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
								break;
							}
						}
					}
				}
			}
			if (!instance.m_currentlyLoading)
			{
				int num8 = Mathf.Max((int)((listenerInfo.m_position.x - 600f) / 320f + 27f), 0);
				int num9 = Mathf.Max((int)((listenerInfo.m_position.z - 600f) / 320f + 27f), 0);
				int num10 = Mathf.Min((int)((listenerInfo.m_position.x + 600f) / 320f + 27f), 53);
				int num11 = Mathf.Min((int)((listenerInfo.m_position.z + 600f) / 320f + 27f), 53);
				for (int k = num9; k <= num11; k++)
				{
					for (int l = num8; l <= num10; l++)
					{
						int num12 = k * 54 + l;
						ushort num13 = this.m_vehicleGrid2[num12];
						int num14 = 0;
						while (num13 != 0)
						{
							this.m_vehicles.m_buffer[(int)num13].PlayAudio(listenerInfo, num13);
							num13 = this.m_vehicles.m_buffer[(int)num13].m_nextGridVehicle;
							if (++num14 >= 16384)
							{
								CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
								break;
							}
						}
					}
				}
			}
			float num15;
			if (instance.m_currentlyLoading || instance2.SimulationPaused || instance3.MuteAll)
			{
				num15 = 0f;
			}
			else
			{
				num15 = instance3.MasterVolume;
			}
			this.m_audioGroup.UpdatePlayers(listenerInfo, num15);
		}
	}

	// Token: 0x06004F1D RID: 20253 RVA: 0x0039D7C0 File Offset: 0x0039BBC0
	public bool CreateVehicle(out ushort vehicle, ref Randomizer r, VehicleInfo info, Vector3 position, TransferManager.TransferReason type, bool transferToSource, bool transferToTarget)
	{
		ushort num;
		if (this.m_vehicles.CreateItem(out num, ref r))
		{
			vehicle = num;
			Vehicle.Frame frame = new Vehicle.Frame(position, Quaternion.identity);
			this.m_vehicles.m_buffer[(int)vehicle].m_flags = Vehicle.Flags.Created;
			this.m_vehicles.m_buffer[(int)vehicle].m_flags2 = (Vehicle.Flags2)0;
			if (transferToSource)
			{
				Vehicle[] buffer = this.m_vehicles.m_buffer;
				ushort num2 = vehicle;
				buffer[(int)num2].m_flags = buffer[(int)num2].m_flags | Vehicle.Flags.TransferToSource;
			}
			if (transferToTarget)
			{
				Vehicle[] buffer2 = this.m_vehicles.m_buffer;
				ushort num3 = vehicle;
				buffer2[(int)num3].m_flags = buffer2[(int)num3].m_flags | Vehicle.Flags.TransferToTarget;
			}
			this.m_vehicles.m_buffer[(int)vehicle].Info = info;
			this.m_vehicles.m_buffer[(int)vehicle].m_frame0 = frame;
			this.m_vehicles.m_buffer[(int)vehicle].m_frame1 = frame;
			this.m_vehicles.m_buffer[(int)vehicle].m_frame2 = frame;
			this.m_vehicles.m_buffer[(int)vehicle].m_frame3 = frame;
			this.m_vehicles.m_buffer[(int)vehicle].m_targetPos0 = Vector4.zero;
			this.m_vehicles.m_buffer[(int)vehicle].m_targetPos1 = Vector4.zero;
			this.m_vehicles.m_buffer[(int)vehicle].m_targetPos2 = Vector4.zero;
			this.m_vehicles.m_buffer[(int)vehicle].m_targetPos3 = Vector4.zero;
			this.m_vehicles.m_buffer[(int)vehicle].m_sourceBuilding = 0;
			this.m_vehicles.m_buffer[(int)vehicle].m_targetBuilding = 0;
			this.m_vehicles.m_buffer[(int)vehicle].m_transferType = (byte)type;
			this.m_vehicles.m_buffer[(int)vehicle].m_transferSize = 0;
			this.m_vehicles.m_buffer[(int)vehicle].m_waitCounter = 0;
			this.m_vehicles.m_buffer[(int)vehicle].m_blockCounter = 0;
			this.m_vehicles.m_buffer[(int)vehicle].m_nextGridVehicle = 0;
			this.m_vehicles.m_buffer[(int)vehicle].m_nextOwnVehicle = 0;
			this.m_vehicles.m_buffer[(int)vehicle].m_nextGuestVehicle = 0;
			this.m_vehicles.m_buffer[(int)vehicle].m_nextLineVehicle = 0;
			this.m_vehicles.m_buffer[(int)vehicle].m_transportLine = 0;
			this.m_vehicles.m_buffer[(int)vehicle].m_leadingVehicle = 0;
			this.m_vehicles.m_buffer[(int)vehicle].m_trailingVehicle = 0;
			this.m_vehicles.m_buffer[(int)vehicle].m_cargoParent = 0;
			this.m_vehicles.m_buffer[(int)vehicle].m_firstCargo = 0;
			this.m_vehicles.m_buffer[(int)vehicle].m_nextCargo = 0;
			this.m_vehicles.m_buffer[(int)vehicle].m_citizenUnits = 0U;
			this.m_vehicles.m_buffer[(int)vehicle].m_path = 0U;
			this.m_vehicles.m_buffer[(int)vehicle].m_lastFrame = 0;
			this.m_vehicles.m_buffer[(int)vehicle].m_pathPositionIndex = 0;
			this.m_vehicles.m_buffer[(int)vehicle].m_lastPathOffset = 0;
			this.m_vehicles.m_buffer[(int)vehicle].m_gateIndex = 0;
			this.m_vehicles.m_buffer[(int)vehicle].m_raceTeam = 0;
			this.m_vehicles.m_buffer[(int)vehicle].m_raceTeammate = 0;
			this.m_vehicles.m_buffer[(int)vehicle].m_racerIndex = 0;
			this.m_vehicles.m_buffer[(int)vehicle].m_waterSource = 0;
			this.m_vehicles.m_buffer[(int)vehicle].m_touristCount = 0;
			this.m_vehicles.m_buffer[(int)vehicle].m_custom = 0;
			info.m_vehicleAI.CreateVehicle(vehicle, ref this.m_vehicles.m_buffer[(int)vehicle]);
			info.m_vehicleAI.FrameDataUpdated(vehicle, ref this.m_vehicles.m_buffer[(int)vehicle], ref this.m_vehicles.m_buffer[(int)vehicle].m_frame0);
			this.m_vehicleCount = (int)(this.m_vehicles.ItemCount() - 1U);
			return true;
		}
		vehicle = 0;
		return false;
	}

	// Token: 0x06004F1E RID: 20254 RVA: 0x0039DC67 File Offset: 0x0039C067
	public void ReleaseVehicle(ushort vehicle)
	{
		this.ReleaseVehicleImplementation(vehicle, ref this.m_vehicles.m_buffer[(int)vehicle]);
	}

	// Token: 0x06004F1F RID: 20255 RVA: 0x0039DC84 File Offset: 0x0039C084
	private void ReleaseVehicleImplementation(ushort vehicle, ref Vehicle data)
	{
		if (data.m_flags != (Vehicle.Flags)0)
		{
			InstanceID instanceID = default(InstanceID);
			instanceID.Vehicle = vehicle;
			Singleton<InstanceManager>.instance.ReleaseInstance(instanceID);
			data.m_flags |= Vehicle.Flags.Deleted;
			data.Unspawn(vehicle);
			VehicleInfo info = data.Info;
			if (info != null)
			{
				info.m_vehicleAI.ReleaseVehicle(vehicle, ref data);
			}
			if (data.m_leadingVehicle != 0)
			{
				if (this.m_vehicles.m_buffer[(int)data.m_leadingVehicle].m_trailingVehicle == vehicle)
				{
					this.m_vehicles.m_buffer[(int)data.m_leadingVehicle].m_trailingVehicle = 0;
				}
				data.m_leadingVehicle = 0;
			}
			if (data.m_trailingVehicle != 0)
			{
				if (this.m_vehicles.m_buffer[(int)data.m_trailingVehicle].m_leadingVehicle == vehicle)
				{
					this.m_vehicles.m_buffer[(int)data.m_trailingVehicle].m_leadingVehicle = 0;
				}
				data.m_trailingVehicle = 0;
			}
			this.ReleaseWaterSource(vehicle, ref data);
			if (data.m_cargoParent != 0)
			{
				ushort num = 0;
				ushort num2 = this.m_vehicles.m_buffer[(int)data.m_cargoParent].m_firstCargo;
				int num3 = 0;
				while (num2 != 0)
				{
					if (num2 == vehicle)
					{
						if (num == 0)
						{
							this.m_vehicles.m_buffer[(int)data.m_cargoParent].m_firstCargo = data.m_nextCargo;
						}
						else
						{
							this.m_vehicles.m_buffer[(int)num].m_nextCargo = data.m_nextCargo;
						}
						break;
					}
					num = num2;
					num2 = this.m_vehicles.m_buffer[(int)num2].m_nextCargo;
					if (++num3 > 16384)
					{
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}
				data.m_cargoParent = 0;
				data.m_nextCargo = 0;
			}
			if (data.m_firstCargo != 0)
			{
				ushort num4 = data.m_firstCargo;
				int num5 = 0;
				while (num4 != 0)
				{
					ushort nextCargo = this.m_vehicles.m_buffer[(int)num4].m_nextCargo;
					this.m_vehicles.m_buffer[(int)num4].m_cargoParent = 0;
					this.m_vehicles.m_buffer[(int)num4].m_nextCargo = 0;
					num4 = nextCargo;
					if (++num5 > 16384)
					{
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}
				data.m_firstCargo = 0;
			}
			if (data.m_path != 0U)
			{
				Singleton<PathManager>.instance.ReleasePath(data.m_path);
				data.m_path = 0U;
			}
			if (data.m_citizenUnits != 0U)
			{
				Singleton<CitizenManager>.instance.ReleaseUnits(data.m_citizenUnits);
				data.m_citizenUnits = 0U;
			}
			data.m_flags = (Vehicle.Flags)0;
			this.m_vehicles.ReleaseItem(vehicle);
			this.m_vehicleCount = (int)(this.m_vehicles.ItemCount() - 1U);
		}
	}

	// Token: 0x06004F20 RID: 20256 RVA: 0x0039DF74 File Offset: 0x0039C374
	private void ReleaseWaterSource(ushort vehicle, ref Vehicle data)
	{
		if (data.m_waterSource != 0)
		{
			Singleton<TerrainManager>.instance.WaterSimulation.ReleaseWaterSource(data.m_waterSource);
			data.m_waterSource = 0;
		}
	}

	// Token: 0x06004F21 RID: 20257 RVA: 0x0039DFA0 File Offset: 0x0039C3A0
	public void AddToGrid(ushort vehicle, ref Vehicle data, bool large)
	{
		Vector3 lastFramePosition = data.GetLastFramePosition();
		if (large)
		{
			int num = Mathf.Clamp((int)(lastFramePosition.x / 320f + 27f), 0, 53);
			int num2 = Mathf.Clamp((int)(lastFramePosition.z / 320f + 27f), 0, 53);
			this.AddToGrid(vehicle, ref data, large, num, num2);
		}
		else
		{
			int num3 = Mathf.Clamp((int)(lastFramePosition.x / 32f + 270f), 0, 539);
			int num4 = Mathf.Clamp((int)(lastFramePosition.z / 32f + 270f), 0, 539);
			this.AddToGrid(vehicle, ref data, large, num3, num4);
		}
	}

	// Token: 0x06004F22 RID: 20258 RVA: 0x0039E054 File Offset: 0x0039C454
	public void AddToGrid(ushort vehicle, ref Vehicle data, bool large, int gridX, int gridZ)
	{
		if (large)
		{
			int num = gridZ * 54 + gridX;
			data.m_nextGridVehicle = this.m_vehicleGrid2[num];
			this.m_vehicleGrid2[num] = vehicle;
		}
		else
		{
			int num2 = gridZ * 540 + gridX;
			data.m_nextGridVehicle = this.m_vehicleGrid[num2];
			this.m_vehicleGrid[num2] = vehicle;
		}
	}

	// Token: 0x06004F23 RID: 20259 RVA: 0x0039E0B0 File Offset: 0x0039C4B0
	public void RemoveFromGrid(ushort vehicle, ref Vehicle data, bool large)
	{
		Vector3 lastFramePosition = data.GetLastFramePosition();
		if (large)
		{
			int num = Mathf.Clamp((int)(lastFramePosition.x / 320f + 27f), 0, 53);
			int num2 = Mathf.Clamp((int)(lastFramePosition.z / 320f + 27f), 0, 53);
			this.RemoveFromGrid(vehicle, ref data, large, num, num2);
		}
		else
		{
			int num3 = Mathf.Clamp((int)(lastFramePosition.x / 32f + 270f), 0, 539);
			int num4 = Mathf.Clamp((int)(lastFramePosition.z / 32f + 270f), 0, 539);
			this.RemoveFromGrid(vehicle, ref data, large, num3, num4);
		}
	}

	// Token: 0x06004F24 RID: 20260 RVA: 0x0039E164 File Offset: 0x0039C564
	public void RemoveFromGrid(ushort vehicle, ref Vehicle data, bool large, int gridX, int gridZ)
	{
		if (large)
		{
			int num = gridZ * 54 + gridX;
			ushort num2 = 0;
			ushort num3 = this.m_vehicleGrid2[num];
			int num4 = 0;
			while (num3 != 0)
			{
				if (num3 == vehicle)
				{
					if (num2 == 0)
					{
						this.m_vehicleGrid2[num] = data.m_nextGridVehicle;
					}
					else
					{
						this.m_vehicles.m_buffer[(int)num2].m_nextGridVehicle = data.m_nextGridVehicle;
					}
					break;
				}
				num2 = num3;
				num3 = this.m_vehicles.m_buffer[(int)num3].m_nextGridVehicle;
				if (++num4 > 16384)
				{
					CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
					break;
				}
			}
			data.m_nextGridVehicle = 0;
		}
		else
		{
			int num5 = gridZ * 540 + gridX;
			ushort num6 = 0;
			ushort num7 = this.m_vehicleGrid[num5];
			int num8 = 0;
			while (num7 != 0)
			{
				if (num7 == vehicle)
				{
					if (num6 == 0)
					{
						this.m_vehicleGrid[num5] = data.m_nextGridVehicle;
					}
					else
					{
						this.m_vehicles.m_buffer[(int)num6].m_nextGridVehicle = data.m_nextGridVehicle;
					}
					break;
				}
				num6 = num7;
				num7 = this.m_vehicles.m_buffer[(int)num7].m_nextGridVehicle;
				if (++num8 > 16384)
				{
					CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
					break;
				}
			}
			data.m_nextGridVehicle = 0;
		}
	}

	// Token: 0x06004F25 RID: 20261 RVA: 0x0039E2E8 File Offset: 0x0039C6E8
	public bool CreateParkedVehicle(out ushort parked, ref Randomizer r, VehicleInfo info, Vector3 position, Quaternion rotation, uint ownerCitizen)
	{
		ushort num;
		if (this.m_parkedVehicles.CreateItem(out num, ref r))
		{
			parked = num;
			this.m_parkedVehicles.m_buffer[(int)parked].m_flags = 9;
			this.m_parkedVehicles.m_buffer[(int)parked].Info = info;
			this.m_parkedVehicles.m_buffer[(int)parked].m_position = position;
			this.m_parkedVehicles.m_buffer[(int)parked].m_rotation = rotation;
			this.m_parkedVehicles.m_buffer[(int)parked].m_ownerCitizen = ownerCitizen;
			this.m_parkedVehicles.m_buffer[(int)parked].m_travelDistance = 0f;
			this.m_parkedVehicles.m_buffer[(int)parked].m_nextGridParked = 0;
			this.AddToGrid(parked, ref this.m_parkedVehicles.m_buffer[(int)parked]);
			this.m_parkedCount = (int)(this.m_parkedVehicles.ItemCount() - 1U);
			return true;
		}
		parked = 0;
		return false;
	}

	// Token: 0x06004F26 RID: 20262 RVA: 0x0039E3EE File Offset: 0x0039C7EE
	public void ReleaseParkedVehicle(ushort parked)
	{
		this.ReleaseParkedVehicleImplementation(parked, ref this.m_parkedVehicles.m_buffer[(int)parked]);
	}

	// Token: 0x06004F27 RID: 20263 RVA: 0x0039E408 File Offset: 0x0039C808
	private void ReleaseParkedVehicleImplementation(ushort parked, ref VehicleParked data)
	{
		if (data.m_flags != 0)
		{
			InstanceID instanceID = default(InstanceID);
			instanceID.ParkedVehicle = parked;
			Singleton<InstanceManager>.instance.ReleaseInstance(instanceID);
			data.m_flags |= 2;
			this.RemoveFromGrid(parked, ref data);
			if (data.m_ownerCitizen != 0U)
			{
				Singleton<CitizenManager>.instance.m_citizens.m_buffer[(int)((UIntPtr)data.m_ownerCitizen)].m_parkedVehicle = 0;
				data.m_ownerCitizen = 0U;
			}
			data.m_flags = 0;
			this.m_parkedVehicles.ReleaseItem(parked);
			this.m_parkedCount = (int)(this.m_parkedVehicles.ItemCount() - 1U);
		}
	}

	// Token: 0x06004F28 RID: 20264 RVA: 0x0039E4AC File Offset: 0x0039C8AC
	public void AddToGrid(ushort parked, ref VehicleParked data)
	{
		int num = Mathf.Clamp((int)(data.m_position.x / 32f + 270f), 0, 539);
		int num2 = Mathf.Clamp((int)(data.m_position.z / 32f + 270f), 0, 539);
		this.AddToGrid(parked, ref data, num, num2);
	}

	// Token: 0x06004F29 RID: 20265 RVA: 0x0039E50C File Offset: 0x0039C90C
	public void AddToGrid(ushort parked, ref VehicleParked data, int gridX, int gridZ)
	{
		int num = gridZ * 540 + gridX;
		data.m_nextGridParked = this.m_parkedGrid[num];
		this.m_parkedGrid[num] = parked;
	}

	// Token: 0x06004F2A RID: 20266 RVA: 0x0039E53C File Offset: 0x0039C93C
	public void RemoveFromGrid(ushort parked, ref VehicleParked data)
	{
		int num = Mathf.Clamp((int)(data.m_position.x / 32f + 270f), 0, 539);
		int num2 = Mathf.Clamp((int)(data.m_position.z / 32f + 270f), 0, 539);
		this.RemoveFromGrid(parked, ref data, num, num2);
	}

	// Token: 0x06004F2B RID: 20267 RVA: 0x0039E59C File Offset: 0x0039C99C
	public void RemoveFromGrid(ushort parked, ref VehicleParked data, int gridX, int gridZ)
	{
		int num = gridZ * 540 + gridX;
		ushort num2 = 0;
		ushort num3 = this.m_parkedGrid[num];
		int num4 = 0;
		while (num3 != 0)
		{
			if (num3 == parked)
			{
				if (num2 == 0)
				{
					this.m_parkedGrid[num] = data.m_nextGridParked;
				}
				else
				{
					this.m_parkedVehicles.m_buffer[(int)num2].m_nextGridParked = data.m_nextGridParked;
				}
				break;
			}
			num2 = num3;
			num3 = this.m_parkedVehicles.m_buffer[(int)num3].m_nextGridParked;
			if (++num4 > 32768)
			{
				CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
				break;
			}
		}
		data.m_nextGridParked = 0;
	}

	// Token: 0x06004F2C RID: 20268 RVA: 0x0039E658 File Offset: 0x0039CA58
	public bool RayCast(Segment3 ray, Vehicle.Flags ignoreFlags, VehicleParked.Flags ignoreFlags2, out Vector3 hit, out ushort vehicleIndex, out ushort parkedIndex)
	{
		hit = ray.b;
		vehicleIndex = 0;
		parkedIndex = 0;
		Bounds bounds;
		bounds..ctor(new Vector3(0f, 512f, 0f), new Vector3(17280f, 1152f, 17280f));
		Segment3 segment = ray;
		if (segment.Clip(bounds))
		{
			Vector3 vector = segment.b - segment.a;
			Vector3 normalized = vector.normalized;
			Vector3 vector2 = segment.a - normalized * 72f;
			Vector3 vector3 = segment.a + Vector3.ClampMagnitude(segment.b - segment.a, 2000f) + normalized * 72f;
			int num = (int)(vector2.x / 32f + 270f);
			int num2 = (int)(vector2.z / 32f + 270f);
			int num3 = (int)(vector3.x / 32f + 270f);
			int num4 = (int)(vector3.z / 32f + 270f);
			float num5 = Mathf.Abs(vector.x);
			float num6 = Mathf.Abs(vector.z);
			int num7;
			int num8;
			if (num5 >= num6)
			{
				num7 = ((vector.x <= 0f) ? (-1) : 1);
				num8 = 0;
				if (num5 != 0f)
				{
					vector *= 32f / num5;
				}
			}
			else
			{
				num7 = 0;
				num8 = ((vector.z <= 0f) ? (-1) : 1);
				if (num6 != 0f)
				{
					vector *= 32f / num6;
				}
			}
			Vector3 vector4 = vector2;
			Vector3 vector5 = vector2;
			do
			{
				Vector3 vector6 = vector5 + vector;
				int num9;
				int num10;
				int num11;
				int num12;
				if (num7 != 0)
				{
					num9 = Mathf.Max(num, 0);
					num10 = Mathf.Min(num, 539);
					num11 = Mathf.Max((int)((Mathf.Min(vector4.z, vector6.z) - 72f) / 32f + 270f), 0);
					num12 = Mathf.Min((int)((Mathf.Max(vector4.z, vector6.z) + 72f) / 32f + 270f), 539);
				}
				else
				{
					num11 = Mathf.Max(num2, 0);
					num12 = Mathf.Min(num2, 539);
					num9 = Mathf.Max((int)((Mathf.Min(vector4.x, vector6.x) - 72f) / 32f + 270f), 0);
					num10 = Mathf.Min((int)((Mathf.Max(vector4.x, vector6.x) + 72f) / 32f + 270f), 539);
				}
				for (int i = num11; i <= num12; i++)
				{
					for (int j = num9; j <= num10; j++)
					{
						ushort num13 = this.m_vehicleGrid[i * 540 + j];
						int num14 = 0;
						while (num13 != 0)
						{
							float num15;
							if (this.m_vehicles.m_buffer[(int)num13].RayCast(num13, segment, ignoreFlags, out num15))
							{
								Vector3 vector7 = segment.Position(num15);
								if (Vector3.SqrMagnitude(vector7 - ray.a) < Vector3.SqrMagnitude(hit - ray.a))
								{
									hit = vector7;
									vehicleIndex = num13;
									parkedIndex = 0;
								}
							}
							num13 = this.m_vehicles.m_buffer[(int)num13].m_nextGridVehicle;
							if (++num14 > 16384)
							{
								CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
								break;
							}
						}
						ushort num16 = this.m_parkedGrid[i * 540 + j];
						int num17 = 0;
						while (num16 != 0)
						{
							float num18;
							if (this.m_parkedVehicles.m_buffer[(int)num16].RayCast(num16, segment, ignoreFlags2, out num18))
							{
								Vector3 vector8 = segment.Position(num18);
								if (Vector3.SqrMagnitude(vector8 - ray.a) < Vector3.SqrMagnitude(hit - ray.a))
								{
									hit = vector8;
									vehicleIndex = 0;
									parkedIndex = num16;
								}
							}
							num16 = this.m_parkedVehicles.m_buffer[(int)num16].m_nextGridParked;
							if (++num17 > 32768)
							{
								CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
								break;
							}
						}
					}
				}
				vector4 = vector5;
				vector5 = vector6;
				num += num7;
				num2 += num8;
			}
			while ((num <= num3 || num7 <= 0) && (num >= num3 || num7 >= 0) && (num2 <= num4 || num8 <= 0) && (num2 >= num4 || num8 >= 0));
		}
		bounds..ctor(new Vector3(0f, 1512f, 0f), new Vector3(17280f, 3024f, 17280f));
		segment = ray;
		if (segment.Clip(bounds))
		{
			Vector3 vector9 = segment.b - segment.a;
			Vector3 normalized2 = vector9.normalized;
			Vector3 vector10 = segment.a - normalized2 * 112f;
			Vector3 vector11 = segment.b + normalized2 * 112f;
			int num19 = (int)(vector10.x / 320f + 27f);
			int num20 = (int)(vector10.z / 320f + 27f);
			int num21 = (int)(vector11.x / 320f + 27f);
			int num22 = (int)(vector11.z / 320f + 27f);
			float num23 = Mathf.Abs(vector9.x);
			float num24 = Mathf.Abs(vector9.z);
			int num25;
			int num26;
			if (num23 >= num24)
			{
				num25 = ((vector9.x <= 0f) ? (-1) : 1);
				num26 = 0;
				if (num23 != 0f)
				{
					vector9 *= 320f / num23;
				}
			}
			else
			{
				num25 = 0;
				num26 = ((vector9.z <= 0f) ? (-1) : 1);
				if (num24 != 0f)
				{
					vector9 *= 320f / num24;
				}
			}
			Vector3 vector12 = vector10;
			Vector3 vector13 = vector10;
			do
			{
				Vector3 vector14 = vector13 + vector9;
				int num27;
				int num28;
				int num29;
				int num30;
				if (num25 != 0)
				{
					num27 = Mathf.Max(num19, 0);
					num28 = Mathf.Min(num19, 53);
					num29 = Mathf.Max((int)((Mathf.Min(vector12.z, vector14.z) - 112f) / 320f + 27f), 0);
					num30 = Mathf.Min((int)((Mathf.Max(vector12.z, vector14.z) + 112f) / 320f + 27f), 53);
				}
				else
				{
					num29 = Mathf.Max(num20, 0);
					num30 = Mathf.Min(num20, 53);
					num27 = Mathf.Max((int)((Mathf.Min(vector12.x, vector14.x) - 112f) / 320f + 27f), 0);
					num28 = Mathf.Min((int)((Mathf.Max(vector12.x, vector14.x) + 112f) / 320f + 27f), 53);
				}
				for (int k = num29; k <= num30; k++)
				{
					for (int l = num27; l <= num28; l++)
					{
						ushort num31 = this.m_vehicleGrid2[k * 54 + l];
						int num32 = 0;
						while (num31 != 0)
						{
							float num33;
							if (this.m_vehicles.m_buffer[(int)num31].RayCast(num31, segment, ignoreFlags, out num33))
							{
								Vector3 vector15 = segment.Position(num33);
								if (Vector3.SqrMagnitude(vector15 - ray.a) < Vector3.SqrMagnitude(hit - ray.a))
								{
									hit = vector15;
									vehicleIndex = num31;
									parkedIndex = 0;
								}
							}
							num31 = this.m_vehicles.m_buffer[(int)num31].m_nextGridVehicle;
							if (++num32 > 16384)
							{
								CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
								break;
							}
						}
					}
				}
				vector12 = vector13;
				vector13 = vector14;
				num19 += num25;
				num20 += num26;
			}
			while ((num19 <= num21 || num25 <= 0) && (num19 >= num21 || num25 >= 0) && (num20 <= num22 || num26 <= 0) && (num20 >= num22 || num26 >= 0));
		}
		if (vehicleIndex != 0 || parkedIndex != 0)
		{
			return true;
		}
		hit = Vector3.zero;
		vehicleIndex = 0;
		parkedIndex = 0;
		return false;
	}

	// Token: 0x06004F2D RID: 20269 RVA: 0x0039EF6C File Offset: 0x0039D36C
	public void UpdateParkedVehicles(float minX, float minZ, float maxX, float maxZ)
	{
		int num = Mathf.Max((int)((minX - 10f) / 32f + 270f), 0);
		int num2 = Mathf.Max((int)((minZ - 10f) / 32f + 270f), 0);
		int num3 = Mathf.Min((int)((maxX + 10f) / 32f + 270f), 539);
		int num4 = Mathf.Min((int)((maxZ + 10f) / 32f + 270f), 539);
		for (int i = num2; i <= num4; i++)
		{
			for (int j = num; j <= num3; j++)
			{
				ushort num5 = this.m_parkedGrid[i * 540 + j];
				int num6 = 0;
				while (num5 != 0)
				{
					if ((this.m_parkedVehicles.m_buffer[(int)num5].m_flags & 4) == 0)
					{
						VehicleParked[] buffer = this.m_parkedVehicles.m_buffer;
						ushort num7 = num5;
						buffer[(int)num7].m_flags = buffer[(int)num7].m_flags | 4;
						this.m_updatedParked[num5 >> 6] |= 1UL << (int)num5;
						this.m_parkedUpdated = true;
					}
					num5 = this.m_parkedVehicles.m_buffer[(int)num5].m_nextGridParked;
					if (++num6 > 32768)
					{
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}
			}
		}
	}

	// Token: 0x06004F2E RID: 20270 RVA: 0x0039F0E4 File Offset: 0x0039D4E4
	protected override void SimulationStepImpl(int subStep)
	{
		if (this.m_parkedUpdated)
		{
			int num = this.m_updatedParked.Length;
			for (int i = 0; i < num; i++)
			{
				ulong num2 = this.m_updatedParked[i];
				if (num2 != 0UL)
				{
					this.m_updatedParked[i] = 0UL;
					for (int j = 0; j < 64; j++)
					{
						if ((num2 & (1UL << j)) != 0UL)
						{
							ushort num3 = (ushort)((i << 6) | j);
							VehicleInfo info = this.m_parkedVehicles.m_buffer[(int)num3].Info;
							VehicleParked[] buffer = this.m_parkedVehicles.m_buffer;
							ushort num4 = num3;
							buffer[(int)num4].m_flags = buffer[(int)num4].m_flags & 65531;
							info.m_vehicleAI.UpdateParkedVehicle(num3, ref this.m_parkedVehicles.m_buffer[(int)num3]);
						}
					}
				}
			}
			this.m_parkedUpdated = false;
		}
		if (subStep != 0)
		{
			SimulationManager instance = Singleton<SimulationManager>.instance;
			Vector3 vector = instance.m_simulationView.m_position + instance.m_simulationView.m_direction * 1000f;
			for (int k = 0; k < 16384; k++)
			{
				Vehicle.Flags flags = this.m_vehicles.m_buffer[k].m_flags;
				if ((flags & Vehicle.Flags.Created) != (Vehicle.Flags)0)
				{
					VehicleInfo info2 = this.m_vehicles.m_buffer[k].Info;
					info2.m_vehicleAI.ExtraSimulationStep((ushort)k, ref this.m_vehicles.m_buffer[k]);
				}
			}
			int num5 = (int)(instance.m_currentFrameIndex & 15U);
			int num6 = num5 * 1024;
			int num7 = (num5 + 1) * 1024 - 1;
			for (int l = num6; l <= num7; l++)
			{
				Vehicle.Flags flags2 = this.m_vehicles.m_buffer[l].m_flags;
				if ((flags2 & Vehicle.Flags.Created) != (Vehicle.Flags)0 && this.m_vehicles.m_buffer[l].m_leadingVehicle == 0)
				{
					VehicleInfo info3 = this.m_vehicles.m_buffer[l].Info;
					info3.m_vehicleAI.SimulationStep((ushort)l, ref this.m_vehicles.m_buffer[l], vector);
				}
			}
			if ((instance.m_currentFrameIndex & 255U) == 0U)
			{
				uint num8 = this.m_maxTrafficFlow / 100U;
				if (num8 == 0U)
				{
					num8 = 1U;
				}
				uint num9 = this.m_totalTrafficFlow / num8;
				if (num9 > 100U)
				{
					num9 = 100U;
				}
				this.m_lastTrafficFlow = num9;
				this.m_totalTrafficFlow = 0U;
				this.m_maxTrafficFlow = 0U;
				this.m_finalVehicleCount = this.m_tempVehicleCount;
				this.m_tempVehicleCount = 0U;
				StatisticsManager instance2 = Singleton<StatisticsManager>.instance;
				StatisticBase statisticBase = instance2.Acquire<StatisticInt32>(StatisticType.TrafficFlow);
				statisticBase.Set((int)num9);
			}
		}
	}

	// Token: 0x06004F2F RID: 20271 RVA: 0x0039F3A8 File Offset: 0x0039D7A8
	public VehicleInfo GetRandomVehicleInfo(ref Randomizer r, ItemClass.Service service, ItemClass.SubService subService, ItemClass.Level level)
	{
		if (!this.m_vehiclesRefreshed)
		{
			CODebugBase<LogChannel>.Error(LogChannel.Core, "Random vehicles not refreshed yet!\n" + Environment.StackTrace);
			return null;
		}
		int num = VehicleManager.GetTransferIndex(service, subService, level);
		FastList<ushort> fastList = this.m_transferVehicles[num];
		if (fastList == null)
		{
			return null;
		}
		if (fastList.m_size == 0)
		{
			return null;
		}
		num = r.Int32((uint)fastList.m_size);
		return PrefabCollection<VehicleInfo>.GetPrefab((uint)fastList.m_buffer[num]);
	}

	// Token: 0x06004F30 RID: 20272 RVA: 0x0039F41C File Offset: 0x0039D81C
	public VehicleInfo GetRandomVehicleInfo(ref Randomizer r, ItemClass.Service service, ItemClass.SubService subService, ItemClass.Level level, VehicleInfo.VehicleType type)
	{
		if (!this.m_vehiclesRefreshed)
		{
			CODebugBase<LogChannel>.Error(LogChannel.Core, "Random vehicles not refreshed yet!\n" + Environment.StackTrace);
			return null;
		}
		int num = VehicleManager.GetTransferIndex(service, subService, level);
		FastList<ushort> fastList = this.m_transferVehicles[num];
		if (fastList == null)
		{
			return null;
		}
		int num2 = this.FindFirst(fastList.m_buffer, fastList.m_size, type);
		int num3 = this.FindFirst(fastList.m_buffer, fastList.m_size, type + 1);
		if (num3 - num2 <= 0)
		{
			return null;
		}
		num = r.Int32(num2, num3 - 1);
		return PrefabCollection<VehicleInfo>.GetPrefab((uint)fastList.m_buffer[num]);
	}

	// Token: 0x06004F31 RID: 20273 RVA: 0x0039F4B8 File Offset: 0x0039D8B8
	private int FindFirst(ushort[] vehicles, int size, VehicleInfo.VehicleType type)
	{
		int num = 0;
		int i = size;
		while (i > num)
		{
			int num2 = i + num >> 1;
			VehicleInfo.VehicleType vehicleType = PrefabCollection<VehicleInfo>.GetPrefab((uint)vehicles[num2]).m_vehicleType;
			if (vehicleType < type)
			{
				num = num2 + 1;
			}
			else
			{
				i = num2;
			}
		}
		return i;
	}

	// Token: 0x06004F32 RID: 20274 RVA: 0x0039F4FC File Offset: 0x0039D8FC
	public void RefreshTransferVehicles()
	{
		if (this.m_vehiclesRefreshed)
		{
			return;
		}
		int num = this.m_transferVehicles.Length;
		for (int i = 0; i < num; i++)
		{
			this.m_transferVehicles[i] = null;
		}
		int num2 = PrefabCollection<VehicleInfo>.PrefabCount();
		for (int j = 0; j < num2; j++)
		{
			VehicleInfo prefab = PrefabCollection<VehicleInfo>.GetPrefab((uint)j);
			if (prefab != null && prefab.m_class.m_service != ItemClass.Service.None && prefab.m_placementStyle == ItemClass.Placement.Automatic)
			{
				int transferIndex = VehicleManager.GetTransferIndex(prefab.m_class.m_service, prefab.m_class.m_subService, prefab.m_class.m_level);
				if (this.m_transferVehicles[transferIndex] == null)
				{
					this.m_transferVehicles[transferIndex] = new FastList<ushort>();
				}
				this.m_transferVehicles[transferIndex].Add((ushort)j);
			}
		}
		for (int k = 0; k < num; k++)
		{
			if (this.m_transferVehicles[k] != null)
			{
				Array.Sort<ushort>(this.m_transferVehicles[k].m_buffer, 0, this.m_transferVehicles[k].m_size, VehicleManager.VehicleTypeComparer.comparer);
			}
		}
		int num3 = 70;
		for (int l = 0; l < num3; l++)
		{
			for (int m = 1; m < 5; m++)
			{
				int num4 = l;
				num4 = num4 * 5 + m;
				FastList<ushort> fastList = this.m_transferVehicles[num4];
				FastList<ushort> fastList2 = this.m_transferVehicles[num4 - 1];
				if (fastList == null && fastList2 != null)
				{
					this.m_transferVehicles[num4] = fastList2;
				}
			}
		}
		this.m_vehiclesRefreshed = true;
	}

	// Token: 0x06004F33 RID: 20275 RVA: 0x0039F698 File Offset: 0x0039DA98
	public IEnumerator<bool> SetVehicleName(ushort vehicleID, string name)
	{
		bool result = false;
		Vehicle.Flags flags = this.m_vehicles.m_buffer[(int)vehicleID].m_flags;
		if (vehicleID != 0 && flags != (Vehicle.Flags)0)
		{
			VehicleInfo info = this.m_vehicles.m_buffer[(int)vehicleID].Info;
			if (info != null)
			{
				result = info.m_vehicleAI.SetVehicleName(vehicleID, ref this.m_vehicles.m_buffer[(int)vehicleID], name);
			}
			if (!result)
			{
				if (!StringExtensions.IsNullOrWhiteSpace(name) && name != this.GenerateVehicleName(vehicleID))
				{
					this.m_vehicles.m_buffer[(int)vehicleID].m_flags = flags | Vehicle.Flags.CustomName;
					InstanceID instanceID = default(InstanceID);
					instanceID.Vehicle = vehicleID;
					Singleton<InstanceManager>.instance.SetName(instanceID, name);
					Singleton<GuideManager>.instance.m_renameNotUsed.Disable();
				}
				else if ((flags & Vehicle.Flags.CustomName) != (Vehicle.Flags)0)
				{
					this.m_vehicles.m_buffer[(int)vehicleID].m_flags = flags & ~Vehicle.Flags.CustomName;
					InstanceID instanceID2 = default(InstanceID);
					instanceID2.Vehicle = vehicleID;
					Singleton<InstanceManager>.instance.SetName(instanceID2, null);
				}
				result = true;
			}
		}
		yield return result;
		yield break;
	}

	// Token: 0x06004F34 RID: 20276 RVA: 0x0039F6C4 File Offset: 0x0039DAC4
	public IEnumerator<bool> SetParkedVehicleName(ushort parkedID, string name)
	{
		bool result = false;
		VehicleParked.Flags flags = (VehicleParked.Flags)this.m_parkedVehicles.m_buffer[(int)parkedID].m_flags;
		if (parkedID != 0 && flags != VehicleParked.Flags.None)
		{
			if (!StringExtensions.IsNullOrWhiteSpace(name) && name != this.GenerateParkedVehicleName(parkedID))
			{
				this.m_parkedVehicles.m_buffer[(int)parkedID].m_flags = (ushort)(flags | VehicleParked.Flags.CustomName);
				InstanceID instanceID = default(InstanceID);
				instanceID.ParkedVehicle = parkedID;
				Singleton<InstanceManager>.instance.SetName(instanceID, name);
				Singleton<GuideManager>.instance.m_renameNotUsed.Disable();
			}
			else if ((flags & VehicleParked.Flags.CustomName) != VehicleParked.Flags.None)
			{
				this.m_parkedVehicles.m_buffer[(int)parkedID].m_flags = (ushort)(flags & ~VehicleParked.Flags.CustomName);
				InstanceID instanceID2 = default(InstanceID);
				instanceID2.ParkedVehicle = parkedID;
				Singleton<InstanceManager>.instance.SetName(instanceID2, null);
			}
			result = true;
		}
		yield return result;
		yield break;
	}

	// Token: 0x06004F35 RID: 20277 RVA: 0x0039F6F0 File Offset: 0x0039DAF0
	public override void UpdateData(SimulationManager.UpdateMode mode)
	{
		Singleton<LoadingManager>.instance.m_loadingProfilerSimulation.BeginLoading("VehicleManager.UpdateData");
		base.UpdateData(mode);
		for (int i = 1; i < 16384; i++)
		{
			if (this.m_vehicles.m_buffer[i].m_flags != (Vehicle.Flags)0 && this.m_vehicles.m_buffer[i].Info == null)
			{
				this.ReleaseVehicle((ushort)i);
			}
		}
		for (int j = 1; j < 32768; j++)
		{
			if (this.m_parkedVehicles.m_buffer[j].m_flags != 0 && this.m_parkedVehicles.m_buffer[j].Info == null)
			{
				this.ReleaseParkedVehicle((ushort)j);
			}
		}
		this.m_infoCount = PrefabCollection<VehicleInfo>.PrefabCount();
		Singleton<LoadingManager>.instance.m_loadingProfilerSimulation.EndLoading();
	}

	// Token: 0x06004F36 RID: 20278 RVA: 0x0039F7DE File Offset: 0x0039DBDE
	public override void GetData(FastList<IDataContainer> data)
	{
		base.GetData(data);
		data.Add(new VehicleManager.Data());
	}

	// Token: 0x06004F37 RID: 20279 RVA: 0x0039F7F4 File Offset: 0x0039DBF4
	public void AirlineModified()
	{
		ushort num = 0;
		while ((int)num < this.m_vehicles.m_buffer.Length)
		{
			VehicleInfo info = this.m_vehicles.m_buffer[(int)num].Info;
			if (info != null)
			{
				PassengerPlaneAI passengerPlaneAI = info.m_vehicleAI as PassengerPlaneAI;
				if (passengerPlaneAI != null)
				{
					passengerPlaneAI.SetAirline(num, ref this.m_vehicles.m_buffer[(int)num]);
				}
			}
			num += 1;
		}
	}

	// Token: 0x06004F38 RID: 20280 RVA: 0x0039F867 File Offset: 0x0039DC67
	public int[] GetBusTypeWeights()
	{
		return this.m_busTypeWeights;
	}

	// Token: 0x06004F39 RID: 20281 RVA: 0x0039F870 File Offset: 0x0039DC70
	public string GetVehicleName(ushort vehicleID)
	{
		if (this.m_vehicles.m_buffer[(int)vehicleID].m_flags != (Vehicle.Flags)0)
		{
			string text = null;
			VehicleInfo info = this.m_vehicles.m_buffer[(int)vehicleID].Info;
			if (info != null)
			{
				text = info.m_vehicleAI.GetVehicleName(vehicleID, ref this.m_vehicles.m_buffer[(int)vehicleID]);
			}
			if (text == null)
			{
				if ((this.m_vehicles.m_buffer[(int)vehicleID].m_flags & Vehicle.Flags.CustomName) != (Vehicle.Flags)0)
				{
					InstanceID instanceID = default(InstanceID);
					instanceID.Vehicle = vehicleID;
					text = Singleton<InstanceManager>.instance.GetName(instanceID);
				}
				if (text == null)
				{
					text = this.GenerateVehicleName(vehicleID);
				}
			}
			return text;
		}
		return null;
	}

	// Token: 0x06004F3A RID: 20282 RVA: 0x0039F92C File Offset: 0x0039DD2C
	public string GetParkedVehicleName(ushort parkedID)
	{
		if (this.m_parkedVehicles.m_buffer[(int)parkedID].m_flags != 0)
		{
			string text = null;
			if ((this.m_parkedVehicles.m_buffer[(int)parkedID].m_flags & 16) != 0)
			{
				InstanceID instanceID = default(InstanceID);
				instanceID.ParkedVehicle = parkedID;
				text = Singleton<InstanceManager>.instance.GetName(instanceID);
			}
			if (text == null)
			{
				text = this.GenerateParkedVehicleName(parkedID);
			}
			return text;
		}
		return null;
	}

	// Token: 0x06004F3B RID: 20283 RVA: 0x0039F9A1 File Offset: 0x0039DDA1
	public string GetDefaultVehicleName(ushort vehicleID)
	{
		return this.GenerateVehicleName(vehicleID);
	}

	// Token: 0x06004F3C RID: 20284 RVA: 0x0039F9AA File Offset: 0x0039DDAA
	public string GetDefaultParkedVehicleName(ushort parkedID)
	{
		return this.GenerateParkedVehicleName(parkedID);
	}

	// Token: 0x06004F3D RID: 20285 RVA: 0x0039F9B4 File Offset: 0x0039DDB4
	private string GenerateVehicleName(ushort vehicleID)
	{
		VehicleInfo info = this.m_vehicles.m_buffer[(int)vehicleID].Info;
		if (info != null)
		{
			string text = PrefabCollection<VehicleInfo>.PrefabName((uint)info.m_prefabDataIndex);
			return Locale.Get("VEHICLE_TITLE", text);
		}
		return "Invalid";
	}

	// Token: 0x06004F3E RID: 20286 RVA: 0x0039F9FC File Offset: 0x0039DDFC
	private string GenerateParkedVehicleName(ushort parkedID)
	{
		VehicleInfo info = this.m_parkedVehicles.m_buffer[(int)parkedID].Info;
		if (info != null)
		{
			string text = PrefabCollection<VehicleInfo>.PrefabName((uint)info.m_prefabDataIndex);
			return Locale.Get("VEHICLE_TITLE", text);
		}
		return "Invalid";
	}

	// Token: 0x06004F3F RID: 20287 RVA: 0x0039FA43 File Offset: 0x0039DE43
	string ISimulationManager.GetName()
	{
		return base.GetName();
	}

	// Token: 0x06004F40 RID: 20288 RVA: 0x0039FA4B File Offset: 0x0039DE4B
	ThreadProfiler ISimulationManager.GetSimulationProfiler()
	{
		return base.GetSimulationProfiler();
	}

	// Token: 0x06004F41 RID: 20289 RVA: 0x0039FA53 File Offset: 0x0039DE53
	void ISimulationManager.SimulationStep(int subStep)
	{
		base.SimulationStep(subStep);
	}

	// Token: 0x06004F42 RID: 20290 RVA: 0x0039FA5C File Offset: 0x0039DE5C
	string IRenderableManager.GetName()
	{
		return base.GetName();
	}

	// Token: 0x06004F43 RID: 20291 RVA: 0x0039FA64 File Offset: 0x0039DE64
	DrawCallData IRenderableManager.GetDrawCallData()
	{
		return base.GetDrawCallData();
	}

	// Token: 0x06004F44 RID: 20292 RVA: 0x0039FA6C File Offset: 0x0039DE6C
	void IRenderableManager.BeginRendering(RenderManager.CameraInfo cameraInfo)
	{
		base.BeginRendering(cameraInfo);
	}

	// Token: 0x06004F45 RID: 20293 RVA: 0x0039FA75 File Offset: 0x0039DE75
	void IRenderableManager.EndRendering(RenderManager.CameraInfo cameraInfo)
	{
		base.EndRendering(cameraInfo);
	}

	// Token: 0x06004F46 RID: 20294 RVA: 0x0039FA7E File Offset: 0x0039DE7E
	void IRenderableManager.BeginOverlay(RenderManager.CameraInfo cameraInfo)
	{
		base.BeginOverlay(cameraInfo);
	}

	// Token: 0x06004F47 RID: 20295 RVA: 0x0039FA87 File Offset: 0x0039DE87
	void IRenderableManager.EndOverlay(RenderManager.CameraInfo cameraInfo)
	{
		base.EndOverlay(cameraInfo);
	}

	// Token: 0x06004F48 RID: 20296 RVA: 0x0039FA90 File Offset: 0x0039DE90
	void IRenderableManager.UndergroundOverlay(RenderManager.CameraInfo cameraInfo)
	{
		base.UndergroundOverlay(cameraInfo);
	}

	// Token: 0x06004F49 RID: 20297 RVA: 0x0039FA99 File Offset: 0x0039DE99
	void IAudibleManager.PlayAudio(AudioManager.ListenerInfo listenerInfo)
	{
		base.PlayAudio(listenerInfo);
	}

	// Token: 0x040051DF RID: 20959
	public const float VEHICLEGRID_CELL_SIZE = 32f;

	// Token: 0x040051E0 RID: 20960
	public const int VEHICLEGRID_RESOLUTION = 540;

	// Token: 0x040051E1 RID: 20961
	public const float VEHICLEGRID_CELL_SIZE2 = 320f;

	// Token: 0x040051E2 RID: 20962
	public const int VEHICLEGRID_RESOLUTION2 = 54;

	// Token: 0x040051E3 RID: 20963
	public const int MAX_VEHICLE_COUNT = 16384;

	// Token: 0x040051E4 RID: 20964
	public const int MAX_PARKED_COUNT = 32768;

	// Token: 0x040051E5 RID: 20965
	public int m_vehicleCount;

	// Token: 0x040051E6 RID: 20966
	public int m_parkedCount;

	// Token: 0x040051E7 RID: 20967
	public int m_infoCount;

	// Token: 0x040051E8 RID: 20968
	[NonSerialized]
	public Array16<Vehicle> m_vehicles;

	// Token: 0x040051E9 RID: 20969
	[NonSerialized]
	public Array16<VehicleParked> m_parkedVehicles;

	// Token: 0x040051EA RID: 20970
	[NonSerialized]
	public ulong[] m_updatedParked;

	// Token: 0x040051EB RID: 20971
	[NonSerialized]
	public bool m_parkedUpdated;

	// Token: 0x040051EC RID: 20972
	[NonSerialized]
	public ushort[] m_vehicleGrid;

	// Token: 0x040051ED RID: 20973
	[NonSerialized]
	public ushort[] m_vehicleGrid2;

	// Token: 0x040051EE RID: 20974
	[NonSerialized]
	public ushort[] m_parkedGrid;

	// Token: 0x040051EF RID: 20975
	[NonSerialized]
	public MaterialPropertyBlock m_materialBlock;

	// Token: 0x040051F0 RID: 20976
	[NonSerialized]
	public int ID_TyreMatrix;

	// Token: 0x040051F1 RID: 20977
	[NonSerialized]
	public int ID_TyrePosition;

	// Token: 0x040051F2 RID: 20978
	[NonSerialized]
	public int ID_LightState;

	// Token: 0x040051F3 RID: 20979
	[NonSerialized]
	public int ID_Color;

	// Token: 0x040051F4 RID: 20980
	[NonSerialized]
	public int ID_ColorV0;

	// Token: 0x040051F5 RID: 20981
	[NonSerialized]
	public int ID_ColorV1;

	// Token: 0x040051F6 RID: 20982
	[NonSerialized]
	public int ID_ColorV2;

	// Token: 0x040051F7 RID: 20983
	[NonSerialized]
	public int ID_ColorV3;

	// Token: 0x040051F8 RID: 20984
	[NonSerialized]
	public int ID_MainTex;

	// Token: 0x040051F9 RID: 20985
	[NonSerialized]
	public int ID_XYSMap;

	// Token: 0x040051FA RID: 20986
	[NonSerialized]
	public int ID_ACIMap;

	// Token: 0x040051FB RID: 20987
	[NonSerialized]
	public int ID_CCCMap;

	// Token: 0x040051FC RID: 20988
	[NonSerialized]
	public int ID_AtlasRect;

	// Token: 0x040051FD RID: 20989
	[NonSerialized]
	public int ID_VehicleTransform;

	// Token: 0x040051FE RID: 20990
	[NonSerialized]
	public int ID_VehicleLightState;

	// Token: 0x040051FF RID: 20991
	[NonSerialized]
	public int ID_VehicleColor;

	// Token: 0x04005200 RID: 20992
	[NonSerialized]
	public int ID_TyreLocation;

	// Token: 0x04005201 RID: 20993
	[NonSerialized]
	public int ID_RacerRect;

	// Token: 0x04005202 RID: 20994
	[NonSerialized]
	public int ID_RacerData;

	// Token: 0x04005203 RID: 20995
	[NonSerialized]
	public AudioGroup m_audioGroup;

	// Token: 0x04005204 RID: 20996
	[NonSerialized]
	public int m_undergroundLayer;

	// Token: 0x04005205 RID: 20997
	[NonSerialized]
	public uint m_totalTrafficFlow;

	// Token: 0x04005206 RID: 20998
	[NonSerialized]
	public uint m_maxTrafficFlow;

	// Token: 0x04005207 RID: 20999
	[NonSerialized]
	public uint m_lastTrafficFlow;

	// Token: 0x04005208 RID: 21000
	[NonSerialized]
	public uint m_tempVehicleCount;

	// Token: 0x04005209 RID: 21001
	[NonSerialized]
	public uint m_finalVehicleCount;

	// Token: 0x0400520A RID: 21002
	public Texture2D m_lodRgbAtlas;

	// Token: 0x0400520B RID: 21003
	public Texture2D m_lodXysAtlas;

	// Token: 0x0400520C RID: 21004
	public Texture2D m_lodAciAtlas;

	// Token: 0x0400520D RID: 21005
	private FastList<ushort>[] m_transferVehicles;

	// Token: 0x0400520E RID: 21006
	private bool m_vehiclesRefreshed;

	// Token: 0x0400520F RID: 21007
	private ulong[] m_renderBuffer;

	// Token: 0x04005210 RID: 21008
	private ulong[] m_renderBuffer2;

	// Token: 0x04005211 RID: 21009
	private int[] m_busTypeWeights;

	// Token: 0x020006A3 RID: 1699
	public class VehicleTypeComparer : IComparer<ushort>
	{
		// Token: 0x06004F4B RID: 20299 RVA: 0x0039FAAC File Offset: 0x0039DEAC
		int IComparer<ushort>.Compare(ushort prefabIndex1, ushort prefabIndex2)
		{
			VehicleInfo prefab = PrefabCollection<VehicleInfo>.GetPrefab((uint)prefabIndex1);
			VehicleInfo prefab2 = PrefabCollection<VehicleInfo>.GetPrefab((uint)prefabIndex2);
			return prefab.m_vehicleType - prefab2.m_vehicleType;
		}

		// Token: 0x04005212 RID: 21010
		public static VehicleManager.VehicleTypeComparer comparer = new VehicleManager.VehicleTypeComparer();
	}

	// Token: 0x020006A4 RID: 1700
	public class Data : IDataContainer
	{
		// Token: 0x06004F4E RID: 20302 RVA: 0x0039FAE8 File Offset: 0x0039DEE8
		public void Serialize(DataSerializer s)
		{
			Singleton<LoadingManager>.instance.m_loadingProfilerSimulation.BeginSerialize(s, "VehicleManager");
			VehicleManager instance = Singleton<VehicleManager>.instance;
			Vehicle[] buffer = instance.m_vehicles.m_buffer;
			VehicleParked[] buffer2 = instance.m_parkedVehicles.m_buffer;
			int num = buffer.Length;
			int num2 = buffer2.Length;
			EncodedArray.UInt @uint = EncodedArray.UInt.BeginWrite(s);
			for (int i = 1; i < num; i++)
			{
				@uint.Write((uint)buffer[i].m_flags);
			}
			for (int j = 1; j < num; j++)
			{
				@uint.Write((uint)buffer[j].m_flags2);
			}
			@uint.EndWrite();
			EncodedArray.UShort @ushort = EncodedArray.UShort.BeginWrite(s);
			for (int k = 1; k < num2; k++)
			{
				@ushort.Write(buffer2[k].m_flags);
			}
			@ushort.EndWrite();
			try
			{
				PrefabCollection<VehicleInfo>.BeginSerialize(s);
				for (int l = 1; l < num; l++)
				{
					if (buffer[l].m_flags != (Vehicle.Flags)0)
					{
						PrefabCollection<VehicleInfo>.Serialize((uint)buffer[l].m_infoIndex);
					}
				}
				for (int m = 1; m < num2; m++)
				{
					if (buffer2[m].m_flags != 0)
					{
						PrefabCollection<VehicleInfo>.Serialize((uint)buffer2[m].m_infoIndex);
					}
				}
			}
			finally
			{
				PrefabCollection<VehicleInfo>.EndSerialize(s);
			}
			EncodedArray.Byte @byte = EncodedArray.Byte.BeginWrite(s);
			for (int n = 1; n < num; n++)
			{
				if (buffer[n].m_flags != (Vehicle.Flags)0)
				{
					@byte.Write(buffer[n].m_gateIndex);
				}
			}
			@byte.EndWrite();
			EncodedArray.UShort ushort2 = EncodedArray.UShort.BeginWrite(s);
			for (int num3 = 1; num3 < num; num3++)
			{
				if (buffer[num3].m_flags != (Vehicle.Flags)0)
				{
					ushort2.Write(buffer[num3].m_raceTeam);
				}
			}
			ushort2.EndWrite();
			EncodedArray.Byte byte2 = EncodedArray.Byte.BeginWrite(s);
			for (int num4 = 1; num4 < num; num4++)
			{
				if (buffer[num4].m_flags != (Vehicle.Flags)0)
				{
					byte2.Write(buffer[num4].m_raceTeammate);
				}
			}
			byte2.EndWrite();
			EncodedArray.Byte byte3 = EncodedArray.Byte.BeginWrite(s);
			for (int num5 = 1; num5 < num; num5++)
			{
				if (buffer[num5].m_flags != (Vehicle.Flags)0)
				{
					byte3.Write(buffer[num5].m_racerIndex);
				}
			}
			byte3.EndWrite();
			EncodedArray.UShort ushort3 = EncodedArray.UShort.BeginWrite(s);
			for (int num6 = 1; num6 < num; num6++)
			{
				if (buffer[num6].m_flags != (Vehicle.Flags)0)
				{
					ushort3.Write(buffer[num6].m_waterSource);
				}
			}
			ushort3.EndWrite();
			for (int num7 = 1; num7 < num; num7++)
			{
				if (buffer[num7].m_flags != (Vehicle.Flags)0)
				{
					Vehicle.Frame lastFrameData = buffer[num7].GetLastFrameData();
					s.WriteVector3(lastFrameData.m_velocity);
					s.WriteVector3(lastFrameData.m_position);
					s.WriteQuaternion(lastFrameData.m_rotation);
					s.WriteFloat(lastFrameData.m_angleVelocity);
					s.WriteVector4(buffer[num7].m_targetPos0);
					s.WriteVector4(buffer[num7].m_targetPos1);
					s.WriteVector4(buffer[num7].m_targetPos2);
					s.WriteVector4(buffer[num7].m_targetPos3);
					s.WriteUInt16((uint)buffer[num7].m_sourceBuilding);
					s.WriteUInt16((uint)buffer[num7].m_targetBuilding);
					s.WriteUInt16((uint)buffer[num7].m_transportLine);
					s.WriteUInt16((uint)buffer[num7].m_transferSize);
					s.WriteUInt8((uint)buffer[num7].m_transferType);
					s.WriteUInt8((uint)buffer[num7].m_waitCounter);
					s.WriteUInt8((uint)buffer[num7].m_blockCounter);
					s.WriteUInt24(buffer[num7].m_citizenUnits);
					s.WriteUInt24(buffer[num7].m_path);
					s.WriteUInt8((uint)buffer[num7].m_pathPositionIndex);
					s.WriteUInt8((uint)buffer[num7].m_lastPathOffset);
					s.WriteUInt16((uint)buffer[num7].m_trailingVehicle);
					s.WriteUInt16((uint)buffer[num7].m_cargoParent);
					s.WriteUInt16((uint)buffer[num7].m_custom);
				}
			}
			for (int num8 = 1; num8 < num2; num8++)
			{
				if (buffer2[num8].m_flags != 0)
				{
					s.WriteVector3(buffer2[num8].m_position);
					s.WriteQuaternion(buffer2[num8].m_rotation);
					s.WriteUInt24(buffer2[num8].m_ownerCitizen);
				}
			}
			s.WriteUInt32(instance.m_totalTrafficFlow);
			s.WriteUInt32(instance.m_maxTrafficFlow);
			s.WriteUInt8(instance.m_lastTrafficFlow);
			s.WriteUInt32(instance.m_tempVehicleCount);
			s.WriteUInt32(instance.m_finalVehicleCount);
			Singleton<LoadingManager>.instance.m_loadingProfilerSimulation.EndSerialize(s, "VehicleManager");
		}

		// Token: 0x06004F4F RID: 20303 RVA: 0x003A0050 File Offset: 0x0039E450
		public void Deserialize(DataSerializer s)
		{
			Singleton<LoadingManager>.instance.m_loadingProfilerSimulation.BeginDeserialize(s, "VehicleManager");
			VehicleManager instance = Singleton<VehicleManager>.instance;
			Vehicle[] buffer = instance.m_vehicles.m_buffer;
			VehicleParked[] buffer2 = instance.m_parkedVehicles.m_buffer;
			ushort[] vehicleGrid = instance.m_vehicleGrid;
			ushort[] vehicleGrid2 = instance.m_vehicleGrid2;
			ushort[] parkedGrid = instance.m_parkedGrid;
			int num = buffer.Length;
			int num2 = buffer2.Length;
			int num3 = vehicleGrid.Length;
			int num4 = vehicleGrid2.Length;
			int num5 = parkedGrid.Length;
			instance.m_vehicles.ClearUnused();
			instance.m_parkedVehicles.ClearUnused();
			for (int i = 0; i < num3; i++)
			{
				vehicleGrid[i] = 0;
			}
			for (int j = 0; j < num4; j++)
			{
				vehicleGrid2[j] = 0;
			}
			for (int k = 0; k < num5; k++)
			{
				parkedGrid[k] = 0;
			}
			for (int l = 0; l < instance.m_updatedParked.Length; l++)
			{
				instance.m_updatedParked[l] = 0UL;
			}
			instance.m_parkedUpdated = false;
			EncodedArray.UInt @uint = EncodedArray.UInt.BeginRead(s);
			for (int m = 1; m < num; m++)
			{
				buffer[m].m_flags = (Vehicle.Flags)@uint.Read();
			}
			if (s.version >= 274U)
			{
				for (int n = 1; n < num; n++)
				{
					buffer[n].m_flags2 = (Vehicle.Flags2)@uint.Read();
				}
			}
			else
			{
				for (int num6 = 1; num6 < num; num6++)
				{
					buffer[num6].m_flags2 = (Vehicle.Flags2)0;
				}
			}
			@uint.EndRead();
			if (s.version >= 205U)
			{
				EncodedArray.UShort @ushort = EncodedArray.UShort.BeginRead(s);
				for (int num7 = 1; num7 < num2; num7++)
				{
					buffer2[num7].m_flags = @ushort.Read();
				}
				@ushort.EndRead();
			}
			else if (s.version >= 115U)
			{
				EncodedArray.UShort ushort2 = EncodedArray.UShort.BeginRead(s);
				for (int num8 = 1; num8 < 16384; num8++)
				{
					buffer2[num8].m_flags = ushort2.Read();
				}
				for (int num9 = 16384; num9 < num2; num9++)
				{
					buffer2[num9].m_flags = 0;
				}
				ushort2.EndRead();
			}
			else
			{
				for (int num10 = 1; num10 < num2; num10++)
				{
					buffer2[num10].m_flags = 0;
				}
			}
			if (s.version >= 30U)
			{
				PrefabCollection<VehicleInfo>.BeginDeserialize(s);
				for (int num11 = 1; num11 < num; num11++)
				{
					if (buffer[num11].m_flags != (Vehicle.Flags)0)
					{
						buffer[num11].m_infoIndex = (ushort)PrefabCollection<VehicleInfo>.Deserialize(true);
					}
				}
				if (s.version >= 115U)
				{
					for (int num12 = 1; num12 < num2; num12++)
					{
						if (buffer2[num12].m_flags != 0)
						{
							buffer2[num12].m_infoIndex = (ushort)PrefabCollection<VehicleInfo>.Deserialize(true);
						}
					}
				}
				PrefabCollection<VehicleInfo>.EndDeserialize(s);
			}
			if (s.version >= 182U)
			{
				EncodedArray.Byte @byte = EncodedArray.Byte.BeginRead(s);
				for (int num13 = 1; num13 < num; num13++)
				{
					if (buffer[num13].m_flags != (Vehicle.Flags)0)
					{
						buffer[num13].m_gateIndex = @byte.Read();
					}
					else
					{
						buffer[num13].m_gateIndex = 0;
					}
				}
				@byte.EndRead();
			}
			else
			{
				for (int num14 = 1; num14 < num; num14++)
				{
					buffer[num14].m_gateIndex = 0;
				}
			}
			if (s.version >= 121008U)
			{
				EncodedArray.UShort ushort3 = EncodedArray.UShort.BeginRead(s);
				for (int num15 = 1; num15 < num; num15++)
				{
					if (buffer[num15].m_flags != (Vehicle.Flags)0)
					{
						buffer[num15].m_raceTeam = ushort3.Read();
					}
					else
					{
						buffer[num15].m_raceTeam = 0;
					}
				}
				ushort3.EndRead();
			}
			else
			{
				for (int num16 = 1; num16 < num; num16++)
				{
					buffer[num16].m_raceTeam = 0;
				}
			}
			if (s.version >= 121012U)
			{
				EncodedArray.Byte byte2 = EncodedArray.Byte.BeginRead(s);
				for (int num17 = 1; num17 < num; num17++)
				{
					if (buffer[num17].m_flags != (Vehicle.Flags)0)
					{
						buffer[num17].m_raceTeammate = byte2.Read();
					}
					else
					{
						buffer[num17].m_raceTeammate = 0;
					}
				}
				byte2.EndRead();
				byte2 = EncodedArray.Byte.BeginRead(s);
				for (int num18 = 1; num18 < num; num18++)
				{
					if (buffer[num18].m_flags != (Vehicle.Flags)0)
					{
						buffer[num18].m_racerIndex = byte2.Read();
					}
					else
					{
						buffer[num18].m_racerIndex = 0;
					}
				}
				byte2.EndRead();
			}
			else
			{
				for (int num19 = 1; num19 < num; num19++)
				{
					buffer[num19].m_raceTeammate = 0;
					buffer[num19].m_racerIndex = 0;
				}
			}
			if (s.version >= 273U)
			{
				EncodedArray.UShort ushort4 = EncodedArray.UShort.BeginRead(s);
				for (int num20 = 1; num20 < num; num20++)
				{
					if (buffer[num20].m_flags != (Vehicle.Flags)0)
					{
						buffer[num20].m_waterSource = ushort4.Read();
					}
					else
					{
						buffer[num20].m_waterSource = 0;
					}
				}
				ushort4.EndRead();
			}
			else
			{
				for (int num21 = 1; num21 < num; num21++)
				{
					buffer[num21].m_waterSource = 0;
				}
			}
			for (int num22 = 1; num22 < num; num22++)
			{
				buffer[num22].m_nextGridVehicle = 0;
				buffer[num22].m_nextGuestVehicle = 0;
				buffer[num22].m_nextOwnVehicle = 0;
				buffer[num22].m_nextLineVehicle = 0;
				buffer[num22].m_leadingVehicle = 0;
				buffer[num22].m_firstCargo = 0;
				buffer[num22].m_nextCargo = 0;
				buffer[num22].m_lastFrame = 0;
				buffer[num22].m_touristCount = 0;
				if (buffer[num22].m_flags != (Vehicle.Flags)0)
				{
					buffer[num22].m_frame0 = new Vehicle.Frame(Vector3.zero, Quaternion.identity);
					if (s.version >= 47U)
					{
						buffer[num22].m_frame0.m_velocity = s.ReadVector3();
					}
					buffer[num22].m_frame0.m_position = s.ReadVector3();
					if (s.version >= 78U)
					{
						buffer[num22].m_frame0.m_rotation = s.ReadQuaternion();
					}
					if (s.version >= 129U)
					{
						buffer[num22].m_frame0.m_angleVelocity = s.ReadFloat();
					}
					buffer[num22].m_frame0.m_underground = (buffer[num22].m_flags & Vehicle.Flags.Underground) != (Vehicle.Flags)0;
					buffer[num22].m_frame0.m_transition = (buffer[num22].m_flags & Vehicle.Flags.Transition) != (Vehicle.Flags)0;
					buffer[num22].m_frame0.m_insideBuilding = (buffer[num22].m_flags & Vehicle.Flags.InsideBuilding) != (Vehicle.Flags)0;
					buffer[num22].m_frame1 = buffer[num22].m_frame0;
					buffer[num22].m_frame2 = buffer[num22].m_frame0;
					buffer[num22].m_frame3 = buffer[num22].m_frame0;
					if (s.version >= 68U)
					{
						buffer[num22].m_targetPos0 = s.ReadVector4();
					}
					else if (s.version >= 47U)
					{
						buffer[num22].m_targetPos0 = s.ReadVector3();
						buffer[num22].m_targetPos0.w = 2f;
					}
					else
					{
						buffer[num22].m_targetPos0 = buffer[num22].m_frame0.m_position;
						buffer[num22].m_targetPos0.w = 2f;
					}
					if (s.version >= 90U)
					{
						buffer[num22].m_targetPos1 = s.ReadVector4();
						buffer[num22].m_targetPos2 = s.ReadVector4();
						buffer[num22].m_targetPos3 = s.ReadVector4();
					}
					else
					{
						buffer[num22].m_targetPos1 = buffer[num22].m_targetPos0;
						buffer[num22].m_targetPos2 = buffer[num22].m_targetPos0;
						buffer[num22].m_targetPos3 = buffer[num22].m_targetPos0;
					}
					buffer[num22].m_sourceBuilding = (ushort)s.ReadUInt16();
					buffer[num22].m_targetBuilding = (ushort)s.ReadUInt16();
					if (s.version >= 52U)
					{
						buffer[num22].m_transportLine = (ushort)s.ReadUInt16();
					}
					else
					{
						buffer[num22].m_transportLine = 0;
					}
					buffer[num22].m_transferSize = (ushort)s.ReadUInt16();
					buffer[num22].m_transferType = (byte)s.ReadUInt8();
					buffer[num22].m_waitCounter = (byte)s.ReadUInt8();
					if (s.version >= 99U)
					{
						buffer[num22].m_blockCounter = (byte)s.ReadUInt8();
					}
					else
					{
						buffer[num22].m_blockCounter = 0;
					}
					if (s.version >= 32U)
					{
						buffer[num22].m_citizenUnits = s.ReadUInt24();
					}
					else
					{
						buffer[num22].m_citizenUnits = 0U;
					}
					if (s.version >= 47U)
					{
						buffer[num22].m_path = s.ReadUInt24();
						buffer[num22].m_pathPositionIndex = (byte)s.ReadUInt8();
						buffer[num22].m_lastPathOffset = (byte)s.ReadUInt8();
					}
					else
					{
						buffer[num22].m_path = 0U;
						buffer[num22].m_pathPositionIndex = 0;
						buffer[num22].m_lastPathOffset = 0;
					}
					if (s.version >= 58U)
					{
						buffer[num22].m_trailingVehicle = (ushort)s.ReadUInt16();
					}
					else
					{
						buffer[num22].m_trailingVehicle = 0;
					}
					if (s.version >= 104U)
					{
						buffer[num22].m_cargoParent = (ushort)s.ReadUInt16();
					}
					else
					{
						buffer[num22].m_cargoParent = 0;
					}
					if (s.version >= 113012U)
					{
						buffer[num22].m_custom = (ushort)s.ReadUInt16();
					}
					else
					{
						buffer[num22].m_custom = 0;
					}
				}
				else
				{
					buffer[num22].m_frame0 = new Vehicle.Frame(Vector3.zero, Quaternion.identity);
					buffer[num22].m_frame1 = new Vehicle.Frame(Vector3.zero, Quaternion.identity);
					buffer[num22].m_frame2 = new Vehicle.Frame(Vector3.zero, Quaternion.identity);
					buffer[num22].m_frame3 = new Vehicle.Frame(Vector3.zero, Quaternion.identity);
					buffer[num22].m_targetPos0 = Vector4.zero;
					buffer[num22].m_targetPos1 = Vector4.zero;
					buffer[num22].m_targetPos2 = Vector4.zero;
					buffer[num22].m_targetPos3 = Vector4.zero;
					buffer[num22].m_sourceBuilding = 0;
					buffer[num22].m_targetBuilding = 0;
					buffer[num22].m_transportLine = 0;
					buffer[num22].m_transferSize = 0;
					buffer[num22].m_transferType = 0;
					buffer[num22].m_waitCounter = 0;
					buffer[num22].m_blockCounter = 0;
					buffer[num22].m_citizenUnits = 0U;
					buffer[num22].m_path = 0U;
					buffer[num22].m_pathPositionIndex = 0;
					buffer[num22].m_lastPathOffset = 0;
					buffer[num22].m_trailingVehicle = 0;
					buffer[num22].m_cargoParent = 0;
					buffer[num22].m_custom = 0;
					instance.m_vehicles.ReleaseItem((ushort)num22);
				}
			}
			if (s.version >= 115U)
			{
				for (int num23 = 1; num23 < num2; num23++)
				{
					buffer2[num23].m_nextGridParked = 0;
					buffer2[num23].m_travelDistance = 0f;
					if (buffer2[num23].m_flags != 0)
					{
						buffer2[num23].m_position = s.ReadVector3();
						buffer2[num23].m_rotation = s.ReadQuaternion();
						buffer2[num23].m_ownerCitizen = s.ReadUInt24();
						if ((buffer2[num23].m_flags & 4) != 0)
						{
							instance.m_updatedParked[num23 >> 6] |= 1UL << num23;
							instance.m_parkedUpdated = true;
						}
					}
					else
					{
						buffer2[num23].m_position = Vector3.zero;
						buffer2[num23].m_rotation = Quaternion.identity;
						buffer2[num23].m_ownerCitizen = 0U;
						instance.m_parkedVehicles.ReleaseItem((ushort)num23);
					}
				}
			}
			else
			{
				for (int num24 = 1; num24 < num2; num24++)
				{
					buffer2[num24].m_nextGridParked = 0;
					buffer2[num24].m_travelDistance = 0f;
					buffer2[num24].m_position = Vector3.zero;
					buffer2[num24].m_rotation = Quaternion.identity;
					buffer2[num24].m_ownerCitizen = 0U;
					instance.m_parkedVehicles.ReleaseItem((ushort)num24);
				}
			}
			if (s.version >= 315U)
			{
				instance.m_totalTrafficFlow = s.ReadUInt32();
				instance.m_maxTrafficFlow = s.ReadUInt32();
				instance.m_lastTrafficFlow = s.ReadUInt8();
			}
			else
			{
				instance.m_totalTrafficFlow = 0U;
				instance.m_maxTrafficFlow = 0U;
				instance.m_lastTrafficFlow = 0U;
			}
			if (s.version >= 116013U)
			{
				instance.m_tempVehicleCount = s.ReadUInt32();
				instance.m_finalVehicleCount = s.ReadUInt32();
			}
			else
			{
				instance.m_tempVehicleCount = 0U;
				instance.m_finalVehicleCount = 0U;
			}
			Singleton<LoadingManager>.instance.m_loadingProfilerSimulation.EndDeserialize(s, "VehicleManager");
		}

		// Token: 0x06004F50 RID: 20304 RVA: 0x003A0F7C File Offset: 0x0039F37C
		public void AfterDeserialize(DataSerializer s)
		{
			Singleton<LoadingManager>.instance.m_loadingProfilerSimulation.BeginAfterDeserialize(s, "VehicleManager");
			CitizenManager instance = Singleton<CitizenManager>.instance;
			VehicleManager instance2 = Singleton<VehicleManager>.instance;
			Singleton<LoadingManager>.instance.WaitUntilEssentialScenesLoaded();
			instance2.m_vehiclesRefreshed = false;
			PrefabCollection<VehicleInfo>.BindPrefabs();
			instance2.RefreshTransferVehicles();
			Vehicle[] buffer = instance2.m_vehicles.m_buffer;
			VehicleParked[] buffer2 = instance2.m_parkedVehicles.m_buffer;
			int num = buffer.Length;
			int num2 = buffer2.Length;
			VehicleInfo vehicleInfo = null;
			VehicleInfo vehicleInfo2 = null;
			if (s.version < 303U)
			{
				int num3 = PrefabCollection<VehicleInfo>.PrefabCount();
				for (int i = 0; i < num3; i++)
				{
					VehicleInfo prefab = PrefabCollection<VehicleInfo>.GetPrefab((uint)i);
					if (prefab != null && prefab.m_prefabDataIndex != -1)
					{
						string text = PrefabCollection<VehicleInfo>.PrefabName((uint)prefab.m_prefabDataIndex);
						if (text == "Evacuation Bus")
						{
							vehicleInfo = prefab;
						}
						else if (text == "Sedan")
						{
							vehicleInfo2 = prefab;
						}
					}
				}
			}
			for (int j = 1; j < num; j++)
			{
				if (buffer[j].m_flags != (Vehicle.Flags)0)
				{
					ushort trailingVehicle = buffer[j].m_trailingVehicle;
					if (trailingVehicle != 0)
					{
						if (buffer[(int)trailingVehicle].m_flags != (Vehicle.Flags)0)
						{
							buffer[(int)trailingVehicle].m_leadingVehicle = (ushort)j;
						}
						else
						{
							buffer[j].m_trailingVehicle = 0;
						}
					}
					ushort cargoParent = buffer[j].m_cargoParent;
					if (cargoParent != 0)
					{
						if (buffer[(int)cargoParent].m_flags != (Vehicle.Flags)0)
						{
							buffer[j].m_nextCargo = buffer[(int)cargoParent].m_firstCargo;
							buffer[(int)cargoParent].m_firstCargo = (ushort)j;
						}
						else
						{
							buffer[j].m_cargoParent = 0;
						}
					}
				}
			}
			for (int k = 1; k < num; k++)
			{
				if (buffer[k].m_flags != (Vehicle.Flags)0)
				{
					if (buffer[k].m_path != 0U)
					{
						PathUnit[] buffer3 = Singleton<PathManager>.instance.m_pathUnits.m_buffer;
						UIntPtr uintPtr = (UIntPtr)buffer[k].m_path;
						buffer3[(int)uintPtr].m_referenceCount = buffer3[(int)uintPtr].m_referenceCount + 1;
					}
					VehicleInfo vehicleInfo3 = buffer[k].Info;
					if (vehicleInfo3 != null)
					{
						if (vehicleInfo3 == vehicleInfo && vehicleInfo2 != null)
						{
							vehicleInfo3 = vehicleInfo2;
						}
						buffer[k].m_infoIndex = (ushort)vehicleInfo3.m_prefabDataIndex;
						vehicleInfo3.m_vehicleAI.LoadVehicle((ushort)k, ref buffer[k]);
						if ((buffer[k].m_flags & Vehicle.Flags.Spawned) != (Vehicle.Flags)0)
						{
							instance2.AddToGrid((ushort)k, ref buffer[k], vehicleInfo3.m_isLargeVehicle);
						}
					}
					int num4 = 0;
					uint num5 = buffer[k].m_citizenUnits;
					int num6 = 0;
					while (num5 != 0U)
					{
						instance.m_units.m_buffer[(int)((UIntPtr)num5)].SetVehicleAfterLoading((ushort)k, ref num4);
						num5 = instance.m_units.m_buffer[(int)((UIntPtr)num5)].m_nextUnit;
						if (++num6 > 524288)
						{
							CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
							break;
						}
					}
					buffer[k].m_touristCount = (ushort)num4;
				}
			}
			for (int l = 1; l < num2; l++)
			{
				if (buffer2[l].m_flags != 0)
				{
					VehicleInfo vehicleInfo4 = buffer2[l].Info;
					if (vehicleInfo4 != null)
					{
						if (vehicleInfo4 == vehicleInfo && vehicleInfo2 != null)
						{
							vehicleInfo4 = vehicleInfo2;
						}
						buffer2[l].m_infoIndex = (ushort)vehicleInfo4.m_prefabDataIndex;
						instance2.AddToGrid((ushort)l, ref buffer2[l]);
					}
					uint ownerCitizen = buffer2[l].m_ownerCitizen;
					if (ownerCitizen != 0U)
					{
						instance.m_citizens.m_buffer[(int)((UIntPtr)ownerCitizen)].m_parkedVehicle = (ushort)l;
					}
				}
			}
			instance2.m_vehicleCount = (int)(instance2.m_vehicles.ItemCount() - 1U);
			instance2.m_parkedCount = (int)(instance2.m_parkedVehicles.ItemCount() - 1U);
			Singleton<LoadingManager>.instance.m_loadingProfilerSimulation.EndAfterDeserialize(s, "VehicleManager");
		}
	}
}
