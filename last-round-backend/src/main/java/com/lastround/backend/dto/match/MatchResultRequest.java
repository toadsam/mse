// file: last-round-backend/src/main/java/com/lastround/backend/dto/match/MatchResultRequest.java
package com.lastround.backend.dto.match;

import jakarta.validation.Valid;
import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotEmpty;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;
import lombok.Getter;
import lombok.Setter;

import java.util.List;

// Request DTO sent by the game host (via Unity) to persist a completed match result.
@Getter
@Setter
public class MatchResultRequest {

    @NotNull
    private Long player1Id;

    @NotNull
    private Long player2Id;

    @NotNull
    private Long winnerId;

    // Score range 0–10 mirrors the in-game round system.
    @NotNull
    @Min(0)
    @Max(10)
    private Integer player1Score;

    @NotNull
    @Min(0)
    @Max(10)
    private Integer player2Score;

    // Exactly two players are expected per match.
    @NotEmpty
    @Size(min = 2, max = 2)
    @Valid
    private List<MatchPlayerResultRequest> players;
}
