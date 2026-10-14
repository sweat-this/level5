using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Level5.Core.Versus;

namespace Level5.BackendV2
{
    /// <summary>Prepares a new draft from fresh participant detail. Creation remains the normal command.</summary>
    public static class CorrespondenceRematch
    {
        public static IEnumerator Prepare(Guid seriesId, CompetitiveRulesetCatalog catalog,
            Action<ChallengeFormState, string> completed)
        {
            long owner = BackendV2SessionStore.IdentityGeneration;
            Guid? player = BackendV2SessionStore.Current?.PlayerId;
            if (!player.HasValue) { completed(null, "sign in to prepare a rematch"); yield break; }
            ApiResponse<SeriesResponseDto> detail = null;
            yield return BackendV2Runtime.Correspondence.Get(seriesId, r => detail = r);
            if (owner != BackendV2SessionStore.IdentityGeneration) { completed(null, "session changed"); yield break; }
            if (detail == null || !detail.Success || detail.Value?.Id != seriesId)
            { completed(null, "could not load completed series: " + BackendV2ErrorMessages.Describe(detail)); yield break; }
            Guid? opponent = CorrespondenceInbox.OpponentId(player.Value, detail.Value.ChallengerId, detail.Value.OpponentId);
            if (detail.Value.Status != "Completed" || !opponent.HasValue || opponent == Guid.Empty)
            { completed(null, "rematch requires completed play by this account"); yield break; }
            ApiResponse<List<FriendSummaryDto>> friends = null;
            yield return BackendV2Runtime.Friends.List(r => friends = r);
            if (owner != BackendV2SessionStore.IdentityGeneration) { completed(null, "session changed"); yield break; }
            if (friends == null || !friends.Success)
            { completed(null, "could not verify friendship: " + BackendV2ErrorMessages.Describe(friends)); yield break; }
            if (friends.Value == null || !friends.Value.Any(f => f.PlayerId == opponent))
            { completed(null, "this opponent must be an accepted friend before a rematch"); yield break; }
            completed(CreateDraft(detail.Value, player.Value, catalog, out string error), error);
        }

        public static ChallengeFormState CreateDraft(SeriesResponseDto detail, Guid player,
            CompetitiveRulesetCatalog catalog, out string error)
        {
            error = null;
            Guid? opponent = detail == null ? null : CorrespondenceInbox.OpponentId(player, detail.ChallengerId, detail.OpponentId);
            if (detail?.Status != "Completed" || !opponent.HasValue || opponent == Guid.Empty)
            { error = "rematch requires completed play by this account"; return null; }
            var rules = detail.Rules;
            CompetitiveRuleset supported = catalog?.Rulesets.FirstOrDefault(r => r.Id.Value == rules?.RulesetId);
            if (rules == null || rules.CompetitionProtocolVersion != 1 || supported == null
                || !supported.SupportsAsync || !supported.CanPlayVersion(rules.RulesetVersion)
                || (rules.InformationPolicy != "SealedAttempt" && rules.InformationPolicy != "OpenTarget")
                || (detail.TotalGames != 1 && detail.TotalGames != 3 && detail.TotalGames != 5 && detail.TotalGames != 7))
            { error = "historic rules or format are not supported by this build; choose a new challenge explicitly"; return null; }
            // The server has no discovery endpoint. Create validates this exact version against its
            // current catalog; rejecting an obsolete version is preferable to silently replacing it.
            return new ChallengeFormState
            {
                OpponentId = opponent,
                RulesetId = rules.RulesetId,
                RulesetVersion = rules.RulesetVersion,
                TotalGames = detail.TotalGames,
                InformationPolicy = rules.InformationPolicy
            };
        }
    }
}
