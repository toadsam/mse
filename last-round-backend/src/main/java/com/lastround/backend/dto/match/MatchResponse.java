// file: last-round-backend/src/main/java/com/lastround/backend/dto/match/MatchResponse.java
package com.lastround.backend.dto.match;

import lombok.Builder;
import lombok.Getter;

import java.time.LocalDateTime;
import java.util.List;

// Response DTO representing a single completed match with both players' summary data.
@Getter
@Builder
public class MatchResponse {
    private Long id;
    private Long player1Id;
    private Long player2Id;
    private Long winnerId;
    private Integer player1Score;
    private Integer player2Score;
    private LocalDateTime createdAt;
    // Per-player detail (stats + augments) for each participant.
    private List<MatchPlayerHistoryResponse> players;
}
