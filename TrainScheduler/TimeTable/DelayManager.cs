using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrainScheduler.TimeTable
{
    /// <summary>
    /// 列車の晩点（遅延）と超速（過速）を管理するクラス。
    /// 各車両の蓄積晩点時間を追跡し、晩点時間が長いほど超速確率が高くなる。
    /// Manages train delay and overspeed per vehicle.
    /// Longer accumulated delay = higher probability of overspeeding on the next leg.
    /// </summary>
    public static class DelayManager
    {
        /// <summary>
        /// The accumulated delay (minutes) at which overspeed probability reaches 100%.
        /// 超速確率が100%になる蓄積晩点時間（分）。
        /// </summary>
        public const int MaxDelayMinutesForFullProbability = 30;

        /// <summary>
        /// Maximum speed multiplier applied when overspeeding at full delay.
        /// 最大晩点時の超速倍率上限。列車の運行速度（m_maxSpeed）を上限として使用する。
        /// </summary>
        public const float MaxSpeedMultiplier = 1.5f;

        /// <summary>
        /// Minutes of accumulated delay to decay when a vehicle departs on schedule.
        /// 定刻出発時に蓄積晩点から差し引く回復量（分）。
        /// </summary>
        public const int DelayRecoveryMinutesPerLeg = 5;

        // vehicleID -> departure time (minutes since midnight) recorded at last departure
        private static readonly Dictionary<ushort, int> previousDepartureMinutes =
            new Dictionary<ushort, int>();

        // vehicleID -> total accumulated delay in minutes
        private static readonly Dictionary<ushort, int> accumulatedDelayMinutes =
            new Dictionary<ushort, int>();

        // vehicleID -> whether this vehicle is flagged to overspeed on the current leg
        private static readonly Dictionary<ushort, bool> isOverspeeding =
            new Dictionary<ushort, bool>();

        // Cities: Skylines simulation runs on a single thread, so a plain Random is safe here.
        // Citiesのシミュレーションはシングルスレッドで動作するため、Randomのスレッドセーフ対応は不要。
        private static readonly System.Random random = new System.Random();

        /// <summary>
        /// Record a vehicle departure. Calculates the delay for this leg and determines
        /// whether the vehicle will overspeed on the next leg.
        /// 車両の出発を記録する。晩点時間を計算し、次区間の超速フラグを決定する。
        /// </summary>
        /// <param name="vehicleId">Leading vehicle ID / 先頭車両ID</param>
        /// <param name="currentTimeMinutes">Current departure time in minutes since midnight</param>
        /// <param name="scheduledDepartures">Departure times at this stop (HHmm format)</param>
        public static void RecordDeparture(ushort vehicleId, int currentTimeMinutes,
            List<string> scheduledDepartures)
        {
            if (scheduledDepartures == null || scheduledDepartures.Count == 0)
                return;

            int legDelay = 0;

            int prevDepMinutes;
            if (previousDepartureMinutes.TryGetValue(vehicleId, out prevDepMinutes))
            {
                // Find the expected departure at this station: the first scheduled departure
                // strictly after the previous station's departure time.
                // 前駅出発後の最初の予定出発時刻を求める（これが本来使うべき出発時刻）。
                int expectedDeparture = GetFirstScheduledStrictlyAfterMinutes(
                    scheduledDepartures, prevDepMinutes);

                if (expectedDeparture >= 0 && currentTimeMinutes > expectedDeparture)
                {
                    // Train departed later than the expected slot → delay on this leg
                    // 本来の出発時刻より後に出発 → この区間で晩点が発生。
                    legDelay = currentTimeMinutes - expectedDeparture;
                }
            }

            // Accumulate delay
            int prevAccumulated;
            int accumulated = accumulatedDelayMinutes.TryGetValue(vehicleId, out prevAccumulated)
                ? prevAccumulated + legDelay
                : legDelay;

            // If no delay this leg, decay accumulated delay slightly so trains that
            // recover can eventually stop overspeeding.
            // この区間が定刻の場合は蓄積晩点を少し減らす（回復処理）。
            if (legDelay == 0 && accumulated > 0)
            {
                accumulated = Math.Max(0, accumulated - DelayRecoveryMinutesPerLeg);
            }

            accumulatedDelayMinutes[vehicleId] = accumulated;

            // Determine overspeed flag for the next leg based on accumulated delay.
            // 蓄積晩点に基づいて次区間の超速フラグを確率的に決定する。
            float probability = Mathf.Min(
                (float)accumulated / MaxDelayMinutesForFullProbability, 1.0f);
            bool overspeed = (accumulated > 0) && (random.NextDouble() < probability);
            isOverspeeding[vehicleId] = overspeed;

            if (overspeed)
            {
                Debug.Log($"[TrainScheduler] Vehicle {vehicleId} is overspeeding " +
                          $"(delay={accumulated}min, probability={probability:P0})");
            }

            // Store current departure as the reference for the next station's calculation.
            previousDepartureMinutes[vehicleId] = currentTimeMinutes;
        }

        /// <summary>
        /// Returns whether the vehicle is currently flagged to overspeed.
        /// 車両が超速フラグを持っているか返す。
        /// </summary>
        public static bool IsOverspeeding(ushort vehicleId)
        {
            bool value;
            return isOverspeeding.TryGetValue(vehicleId, out value) && value;
        }

        /// <summary>
        /// Returns the speed multiplier for the vehicle.
        /// Scales linearly with accumulated delay from 1.0 up to MaxSpeedMultiplier.
        /// 蓄積晩点時間に比例して1.0〜MaxSpeedMultiplierの速度倍率を返す。
        /// </summary>
        public static float GetSpeedMultiplier(ushort vehicleId)
        {
            bool overspeed;
            if (!isOverspeeding.TryGetValue(vehicleId, out overspeed) || !overspeed)
                return 1.0f;

            int delay;
            if (!accumulatedDelayMinutes.TryGetValue(vehicleId, out delay) || delay <= 0)
                return 1.0f;

            float delayFraction = Mathf.Min((float)delay / MaxDelayMinutesForFullProbability, 1.0f);
            return 1.0f + (MaxSpeedMultiplier - 1.0f) * delayFraction;
        }

        /// <summary>
        /// Returns the accumulated delay in minutes for the vehicle.
        /// 車両の蓄積晩点時間（分）を返す。
        /// </summary>
        public static int GetAccumulatedDelay(ushort vehicleId)
        {
            int delay;
            return accumulatedDelayMinutes.TryGetValue(vehicleId, out delay) ? delay : 0;
        }

        /// <summary>
        /// Clears all delay tracking state for a specific vehicle (e.g., when it despawns).
        /// 特定の車両の晩点追跡データをクリアする。
        /// </summary>
        public static void ClearVehicle(ushort vehicleId)
        {
            previousDepartureMinutes.Remove(vehicleId);
            accumulatedDelayMinutes.Remove(vehicleId);
            isOverspeeding.Remove(vehicleId);
        }

        /// <summary>
        /// Clears all delay tracking state (called on level unload).
        /// レベルアンロード時に全車両のデータをクリアする。
        /// </summary>
        public static void ClearAll()
        {
            previousDepartureMinutes.Clear();
            accumulatedDelayMinutes.Clear();
            isOverspeeding.Clear();
        }

        /// <summary>
        /// Converts an HHmm format string to minutes since midnight.
        /// HHmm形式の文字列を午前0時からの分数に変換する。
        /// </summary>
        public static int HHmmToMinutes(string hhmm)
        {
            if (string.IsNullOrEmpty(hhmm) || hhmm.Length < 4)
                return -1;
            int hours, minutes;
            if (!int.TryParse(hhmm.Substring(0, 2), out hours) ||
                !int.TryParse(hhmm.Substring(2, 2), out minutes))
                return -1;
            return hours * 60 + minutes;
        }

        /// <summary>
        /// Returns the first scheduled departure time (in minutes) strictly after
        /// the given reference time, or -1 if none found.
        /// 指定した参照時刻より後の最初の予定出発時刻（分）を返す。見つからない場合は-1。
        /// </summary>
        private static int GetFirstScheduledStrictlyAfterMinutes(
            List<string> departures, int afterMinutes)
        {
            int result = int.MaxValue;
            foreach (string dep in departures)
            {
                int depMinutes = HHmmToMinutes(dep);
                if (depMinutes < 0) continue;
                if (depMinutes > afterMinutes && depMinutes < result)
                    result = depMinutes;
            }
            return result == int.MaxValue ? -1 : result;
        }
    }
}
