namespace Alterverse.Profile
{
    // A dismissal differs from a player confirming they already rated. The legacy
    // v1 "offered once" flag must never be treated as a submitted review.
    public static class ReviewPromptProgress
    {
        public const string PendingMatchKey = "DF_ReviewPromptPendingMatch_v2";
        private const string LastOfferedMatchKey = "DF_ReviewPromptLastOfferedMatch_v2";
        private const string RatedKey = "DF_ReviewPromptRated_v2";

        public static bool AlreadyRated => AccountProfilePrefs.GetInt(RatedKey, 0) == 1;

        // Called inside the existing receipt-safe completed-match transaction.
        public static bool ShouldQueue(int completedMatches) => !AlreadyRated &&
            completedMatches >= 2 && completedMatches % 2 == 0 &&
            completedMatches > AccountProfilePrefs.GetInt(LastOfferedMatchKey, 0);

        public static bool ShouldOffer(int completedMatches)
        {
            int pending = AccountProfilePrefs.GetInt(PendingMatchKey, 0);
            return !AlreadyRated && pending >= 2 && pending <= completedMatches &&
                pending > AccountProfilePrefs.GetInt(LastOfferedMatchKey, 0);
        }

        public static void MarkOffered(int completedMatches)
        {
            AccountProfilePrefs.SetInt(LastOfferedMatchKey, completedMatches);
            AccountProfilePrefs.SetInt(PendingMatchKey, 0);
            AccountProfilePrefs.Save();
        }

        public static void ConfirmAlreadyRated()
        {
            AccountProfilePrefs.SetInt(RatedKey, 1);
            AccountProfilePrefs.SetInt(PendingMatchKey, 0);
            AccountProfilePrefs.Save();
        }
    }
}
