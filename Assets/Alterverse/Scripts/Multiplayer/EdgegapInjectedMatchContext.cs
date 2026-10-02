using System;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Reads Edgegap Matchmaker variables injected into a deployment and validates connecting
    /// player tickets against the exact assigned match/team. Local non-Edgegap dedicated-server
    /// tests intentionally fall back to the claimed values because injected context is unavailable.
    /// </summary>
    public static class EdgegapInjectedMatchContext
    {
        private const string DeploymentIdVariable = "ARBITRIUM_REQUEST_ID";
        private const string TicketIdsVariable = "MM_TICKET_IDS";
        private const string TicketPrefix = "MM_TICKET_";
        private const string MatchIdVariable = "MM_MATCH_ID";

        [Serializable]
        private sealed class TicketData
        {
            public string id;
            public string group_id;
            public string team_id;
        }

        public static bool IsEdgegapDeployment =>
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(DeploymentIdVariable));

        public static string InjectedMatchId =>
            (Environment.GetEnvironmentVariable(MatchIdVariable) ?? "").Trim();

        public static bool TryValidateTicket(
            string ticketId,
            string claimedTeamId,
            string claimedMatchId,
            out string authoritativeTeamId,
            out string authoritativeMatchId,
            out string reason)
        {
            authoritativeTeamId = (claimedTeamId ?? "").Trim();
            authoritativeMatchId = (claimedMatchId ?? "").Trim();
            reason = "";

            ticketId = (ticketId ?? "").Trim();
            if (string.IsNullOrWhiteSpace(ticketId))
            {
                reason = "ticket id is empty";
                return false;
            }

            // Local developer builds do not receive Edgegap deployment variables. Keep those usable
            // for loopback/LAN tests, but become strict as soon as Edgegap deployment context exists.
            if (!IsEdgegapDeployment)
            {
                if (string.IsNullOrWhiteSpace(authoritativeTeamId))
                {
                    reason = "team id is empty";
                    return false;
                }
                return true;
            }

            string ticketIdsJson = Environment.GetEnvironmentVariable(TicketIdsVariable) ?? "";
            if (!JsonStringArrayContains(ticketIdsJson, ticketId))
            {
                reason = "ticket is not assigned to this Edgegap deployment";
                return false;
            }

            string ticketJson = Environment.GetEnvironmentVariable(TicketPrefix + ticketId) ?? "";
            if (string.IsNullOrWhiteSpace(ticketJson))
            {
                reason = "injected ticket payload is missing";
                return false;
            }

            TicketData ticket;
            try
            {
                ticket = JsonUtility.FromJson<TicketData>(ticketJson);
            }
            catch (Exception ex)
            {
                reason = "injected ticket payload could not be parsed: " + ex.Message;
                return false;
            }

            if (ticket == null || string.IsNullOrWhiteSpace(ticket.id) ||
                !string.Equals(ticket.id.Trim(), ticketId, StringComparison.Ordinal))
            {
                reason = "injected ticket id does not match";
                return false;
            }

            authoritativeTeamId = (ticket.team_id ?? "").Trim();
            if (string.IsNullOrWhiteSpace(authoritativeTeamId))
            {
                reason = "injected ticket has no team id";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(claimedTeamId) &&
                !string.Equals(authoritativeTeamId, claimedTeamId.Trim(), StringComparison.Ordinal))
            {
                reason = "client team does not match Edgegap assignment";
                return false;
            }

            string injectedMatchId = InjectedMatchId;
            if (!string.IsNullOrWhiteSpace(injectedMatchId))
            {
                authoritativeMatchId = injectedMatchId;
                if (!string.IsNullOrWhiteSpace(claimedMatchId) &&
                    !string.Equals(injectedMatchId, claimedMatchId.Trim(), StringComparison.Ordinal))
                {
                    reason = "client match does not match Edgegap assignment";
                    return false;
                }
            }

            return true;
        }

        private static bool JsonStringArrayContains(string json, string expected)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrEmpty(expected)) return false;

            // MM_TICKET_IDS is a JSON string array. Avoid a third-party JSON dependency and parse
            // quoted JSON strings conservatively, including standard backslash escapes.
            bool inString = false;
            bool escaping = false;
            var buffer = new System.Text.StringBuilder(128);
            for (int i = 0; i < json.Length; i++)
            {
                char ch = json[i];
                if (!inString)
                {
                    if (ch == '"')
                    {
                        inString = true;
                        escaping = false;
                        buffer.Clear();
                    }
                    continue;
                }

                if (escaping)
                {
                    switch (ch)
                    {
                        case '"': buffer.Append('"'); break;
                        case '\\': buffer.Append('\\'); break;
                        case '/': buffer.Append('/'); break;
                        case 'b': buffer.Append('\b'); break;
                        case 'f': buffer.Append('\f'); break;
                        case 'n': buffer.Append('\n'); break;
                        case 'r': buffer.Append('\r'); break;
                        case 't': buffer.Append('\t'); break;
                        default: buffer.Append(ch); break;
                    }
                    escaping = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escaping = true;
                    continue;
                }

                if (ch == '"')
                {
                    inString = false;
                    if (string.Equals(buffer.ToString(), expected, StringComparison.Ordinal)) return true;
                    continue;
                }

                buffer.Append(ch);
            }
            return false;
        }
    }
}
