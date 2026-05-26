// file: last-round-backend/src/main/java/com/lastround/backend/dto/match/MatchResponse.java
package com.lastround.backend.dto.match;

import lombok.Builder;
import lombok.Getter;

import java.time.LocalDateTime;
import java.util.List;

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
    private List<MatchPlayerHistoryResponse> players;
}
