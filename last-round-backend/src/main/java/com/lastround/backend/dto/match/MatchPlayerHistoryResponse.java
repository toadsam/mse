package com.lastround.backend.dto.match;

import lombok.Builder;
import lombok.Getter;

import java.time.LocalDateTime;
import java.util.List;

// Response DTO for one player's performance detail within a single match history entry.
@Getter
@Builder
public class MatchPlayerHistoryResponse {
    private Long userId;
    private String nickname;
    // result is either "WIN" or "LOSE".
    private String result;
    private Integer score;
    private Integer damageDealt;
    private String characterName;
    private LocalDateTime createdAt;
    // Augments chosen by this player during the match, ordered by selectedOrder.
    private List<MatchPlayerAugmentResponse> augments;
}