using Guildwright.Core.Adventurers;

namespace Guildwright.Core.Careers;

/// <summary>
/// 선배가 후배 육성에 주는 보너스.
/// <para>
/// 이 시스템의 목적은 <b>손실을 자산으로 바꾸는 것</b>입니다.
/// 은퇴한(또는 불구가 된) 모험가가 사라지지 않고 길드에 남아 후배를 키우면,
/// 오래 데리고 있던 캐릭터를 떠나보내는 일이 순수한 상실이 아니게 됩니다.
/// </para>
/// <para>
/// 그리고 <b>실전을 오래 살아남은 멘토일수록 사람 보는 눈이 좋습니다</b> —
/// 감정 정확도 보너스가 여기서 나옵니다. 실전 리스크를 감수한 것에 대한 장기 보상입니다.
/// </para>
/// 근거: docs/01-game-design.md §5.4
/// </summary>
/// <param name="TrainingMultiplier">훈련 성장 배율.</param>
/// <param name="AppraisalBonus">감정 역량 가산 (0.0~1.0).</param>
/// <param name="MentorName">표시용 이름.</param>
public sealed record Mentorship(double TrainingMultiplier, double AppraisalBonus, string MentorName)
{
    public static readonly Mentorship None = new(1.0, 0.0, "없음");

    /// <summary>은퇴한 모험가로부터 멘토십을 만듭니다.</summary>
    /// <exception cref="ArgumentException">아직 현역이거나 사망한 경우.</exception>
    public static Mentorship From(Adventurer mentor)
    {
        if (!mentor.CanMentor)
        {
            throw new ArgumentException(
                $"{mentor.Name}은(는) 멘토가 될 수 없습니다 (상태: {mentor.Status}). " +
                "살아서 은퇴했거나 불구가 된 모험가만 멘토가 됩니다.",
                nameof(mentor));
        }

        // 능력치가 높을수록, 실전을 많이 겪었을수록 잘 가르칩니다.
        double fromStats = Math.Min(0.20, mentor.Stats.Total / 1800.0);
        double fromExperience = Math.Min(0.20, mentor.DeploymentYears * 0.025);
        double training = 1.0 + fromStats + fromExperience;

        // 실전을 오래 살아남은 사람이 재능을 알아봅니다.
        double appraisal = Math.Min(0.55, mentor.DeploymentYears * 0.06 + mentor.Judgement / 400.0);

        return new Mentorship(training, appraisal, mentor.Name);
    }
}
