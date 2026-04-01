// file: last-round-backend/src/main/java/com/lastround/backend/dto/match/MatchResultRequest.java
package com.lastround.backend.dto.match;

import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotNull;
import lombok.Getter;
import lombok.Setter;

@Getter
@Setter
public class MatchResultRequest {

    @NotNull
    private Long player1Id;

    @NotNull
    private Long player2Id;

    @NotNull
    private Long winnerId;

    @NotNull
    @Min(0)
    @Max(10)
    private Integer player1Score;

    @NotNull
    @Min(0)
    @Max(10)
    private Integer player2Score;
}
