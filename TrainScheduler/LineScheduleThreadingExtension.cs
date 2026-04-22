using ColossalFramework;
using ICities;
using TrainScheduler.TimeTable;

namespace TrainScheduler
{
    /// <summary>
    /// ゲームのシミュレーションループにフックして、毎ゲーム分ごとに路線の自動開閉を行う。
    /// ICities の ThreadingExtensionBase を継承することで自動的にゲームから呼ばれる。
    /// </summary>
    public class LineScheduleThreadingExtension : ThreadingExtensionBase
    {
        private string _lastCheckedTime = string.Empty;

        public override void OnUpdate(float realTimeDelta, float simulationTimeDelta)
        {
            // シミュレーションが停止中（ポーズ中）はスキップ
            if (simulationTimeDelta <= 0f) return;

            // 時刻表が未ロードの場合はスキップ
            if (TimeTableManager.TimeTable == null) return;

            // ゲーム内時刻を HHmm 形式で取得し、分が変わったときだけ処理する。
            // DateTime.ToString("HHmm") は既存の PassengerTrainAICanLeave でも使われている同じ形式。
            var currentTime = SimulationManager.instance.m_currentGameTime.ToString("HHmm");
            if (currentTime == _lastCheckedTime) return;
            _lastCheckedTime = currentTime;

            LineScheduleService.CheckAndUpdateLineState(currentTime);
        }
    }
}
