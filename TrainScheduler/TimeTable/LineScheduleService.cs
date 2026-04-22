using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TrainScheduler.TimeTable
{
    /// <summary>
    /// 時刻表の末班車時刻を過ぎたら自動的に路線をStoppedにして市民が乗れないようにし、
    /// 始発時刻になったら再び路線を有効化するサービス。
    /// LineRecord.AutoClose = true の路線のみ対象。
    /// 注意: 日付をまたぐ（例: 始発22:00 → 終発06:00）運行には対応していない。
    /// </summary>
    public static class LineScheduleService
    {
        // このMODが自動的にStoppedにした路線IDのセット
        private static readonly HashSet<string> _autoClosedLines = new HashSet<string>();

        /// <summary>
        /// 保持している自動クローズ状態をリセットし、閉じていた路線を再度開放する。
        /// OnLevelUnloading時に呼ぶこと。
        /// </summary>
        public static void Reset()
        {
            foreach (var lineIdStr in _autoClosedLines)
            {
                try
                {
                    ushort lineId = Convert.ToUInt16(lineIdStr);
                    TransportManager.instance.m_lines.m_buffer[lineId].m_flags &= ~TransportLine.Flags.Stopped;
                }
                catch (Exception e)
                {
                    Debug.Log($"[TrainScheduler] LineScheduleService.Reset: failed to reopen line {lineIdStr}: {e.Message}");
                }
            }
            _autoClosedLines.Clear();
        }

        /// <summary>
        /// 現在のゲーム内時刻に基づいて各路線のStoppedフラグを更新する。
        /// ThreadingExtensionから毎ゲーム分ごとに呼ぶこと。
        /// </summary>
        /// <param name="currentTime">現在のゲーム内時刻 (HHmm形式, 例: "0630")</param>
        public static void CheckAndUpdateLineState(string currentTime)
        {
            if (TimeTableManager.TimeTable == null) return;

            foreach (var line in TimeTableManager.TimeTable)
            {
                if (!line.Enabled || !line.AutoClose) continue;
                if (line.Stops == null || line.Stops.Count == 0) continue;

                ushort lineId;
                if (!ushort.TryParse(line.LineID, out lineId)) continue;

                string closeTime = GetLineCloseTime(line);
                string openTime = GetLineOpenTime(line);

                if (string.IsNullOrEmpty(closeTime) || string.IsNullOrEmpty(openTime)) continue;

                // 日付またぎのスケジュール（openTime >= closeTime）は対応外のためスキップ
                if (string.Compare(openTime, closeTime, StringComparison.Ordinal) >= 0) continue;

                bool isAutoClosedByUs = _autoClosedLines.Contains(line.LineID);

                if (!isAutoClosedByUs && string.Compare(currentTime, closeTime, StringComparison.Ordinal) > 0)
                {
                    // 末班車発車時刻を過ぎた（1分後）→ 路線をStoppedにする。
                    // '>=' ではなく '>' を使うことで末班車が確実に発車した後にクローズする。
                    TransportManager.instance.m_lines.m_buffer[lineId].m_flags |= TransportLine.Flags.Stopped;
                    _autoClosedLines.Add(line.LineID);
                    Debug.Log($"[TrainScheduler] Line {lineId} auto-closed at {currentTime} (last departure: {closeTime})");
                }
                else if (isAutoClosedByUs
                    && string.Compare(currentTime, openTime, StringComparison.Ordinal) >= 0
                    && string.Compare(currentTime, closeTime, StringComparison.Ordinal) < 0)
                {
                    // 始発時刻になり、かつまだ営業終了前（closeTime未満）→ Stoppedを解除して再開放。
                    // 'currentTime < closeTime' の確認は、closeTime経過後の深夜帯に誤って再開放しないために必要。
                    // 例: closeTime="2350", openTime="0600" の場合、"2355" で isAutoClosedByUs=true のままだと
                    // このチェックなしでは '2355 >= 0600' が true になり誤再開放される。
                    TransportManager.instance.m_lines.m_buffer[lineId].m_flags &= ~TransportLine.Flags.Stopped;
                    _autoClosedLines.Remove(line.LineID);
                    Debug.Log($"[TrainScheduler] Line {lineId} auto-reopened at {currentTime} (first departure: {openTime})");
                }
            }
        }

        /// <summary>
        /// 路線内の全停車場の最終発車時刻（最大値）を返す。
        /// </summary>
        private static string GetLineCloseTime(LineRecord line)
        {
            string maxTime = null;
            IEnumerable<StopRecord> stops = line.Mode == "FirstToAll"
                ? line.Stops.Take(1)
                : (IEnumerable<StopRecord>)line.Stops;

            foreach (var stop in stops)
            {
                if (!stop.Enabled || stop.Departures == null || stop.Departures.Count == 0) continue;
                var lastDep = stop.Departures.Last();
                if (maxTime == null || string.Compare(lastDep, maxTime, StringComparison.Ordinal) > 0)
                    maxTime = lastDep;
            }
            return maxTime;
        }

        /// <summary>
        /// 路線内の全停車場の最初の発車時刻（最小値）を返す。
        /// </summary>
        private static string GetLineOpenTime(LineRecord line)
        {
            string minTime = null;
            IEnumerable<StopRecord> stops = line.Mode == "FirstToAll"
                ? line.Stops.Take(1)
                : (IEnumerable<StopRecord>)line.Stops;

            foreach (var stop in stops)
            {
                if (!stop.Enabled || stop.Departures == null || stop.Departures.Count == 0) continue;
                var firstDep = stop.Departures.First();
                if (minTime == null || string.Compare(firstDep, minTime, StringComparison.Ordinal) < 0)
                    minTime = firstDep;
            }
            return minTime;
        }
    }
}
