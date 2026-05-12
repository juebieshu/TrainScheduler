using HarmonyLib;
using TrainScheduler.TimeTable;
using UnityEngine;

namespace TrainScheduler.AIExtend.PassengerTrainAI
{
    /// <summary>
    /// Harmony patch for PassengerTrainAI.SimulationStep.
    /// When a vehicle is flagged as overspeeding (due to accumulated delay),
    /// the velocity is scaled up proportionally in the Postfix.
    /// 
    /// 晩点超速機能：晩点が蓄積した車両に対して SimulationStep の Postfix で速度を増加させる。
    /// 晩点が大きいほど超速の確率・倍率が上がり、最大で列車の運行速度（m_maxSpeed）まで増速する。
    /// </summary>
    [HarmonyPatch(typeof(global::PassengerTrainAI), "SimulationStep")]
    public static class PassengerTrainAISimulationStep
    {
        /// <summary>
        /// Minimum speed (in game units/s) below which no velocity boost is applied.
        /// Prevents division by zero and avoids boosting nearly-stopped trains.
        /// ほぼ停止している車両への誤適用を防ぐための最低速度閾値（ゲーム単位/秒）。
        /// </summary>
        private const float MinimumSpeedThreshold = 0.01f;

        /// <summary>
        /// Postfix: apply speed boost to moving, non-stopped leading vehicles that are
        /// flagged for overspeeding by the DelayManager.
        /// 走行中の先頭車両に対して超速フラグが立っている場合に速度を増加させる。
        /// </summary>
        public static void Postfix(
            ushort vehicleID,
            ref Vehicle vehicleData,
            ref Vehicle.Frame frameData,
            ushort leaderID,
            ref Vehicle leaderData,
            int lodPhysics)
        {
            // Only apply to the leading vehicle (trailing cars follow automatically)
            // 先頭車両のみ処理（牽引車両は自動的に追従するため対象外）
            if (vehicleData.m_leadingVehicle != 0)
                return;

            // Only apply when the vehicle is moving (not stopped at a station)
            // 走行中のみ（停車中は対象外）
            if ((vehicleData.m_flags & Vehicle.Flags.Stopped) != (Vehicle.Flags)0)
                return;

            // Check if this vehicle is flagged for overspeeding
            // 超速フラグが立っていない場合は処理しない
            if (!DelayManager.IsOverspeeding(vehicleID))
                return;

            float currentSpeed = frameData.m_velocity.magnitude;
            if (currentSpeed < MinimumSpeedThreshold)
                return;

            float multiplier = DelayManager.GetSpeedMultiplier(vehicleID);
            if (multiplier <= 1.0f)
                return;

            float targetSpeed = currentSpeed * multiplier;

            // Cap the boosted speed at the vehicle's own maximum speed (m_maxSpeed),
            // which represents the train's rated operating speed.
            // 速度上限は列車の運行速度（m_maxSpeed）とする。
            float maxAllowedSpeed = vehicleData.Info != null
                ? vehicleData.Info.m_maxSpeed
                : targetSpeed;

            if (targetSpeed > maxAllowedSpeed)
                targetSpeed = maxAllowedSpeed;

            // Apply the boosted velocity along the same direction
            // 方向を保ちながら速度を増加させる
            if (targetSpeed > currentSpeed)
            {
                frameData.m_velocity = frameData.m_velocity.normalized * targetSpeed;

#if DEBUG
                Debug.Log($"[TrainScheduler] Vehicle {vehicleID} overspeeding: " +
                          $"{currentSpeed:F2} -> {targetSpeed:F2} (x{multiplier:F2}), " +
                          $"delay={DelayManager.GetAccumulatedDelay(vehicleID)}min");
#endif
            }
        }
    }
}
