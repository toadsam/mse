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

    @NotEmpty
    @Size(min = 2, max = 2)
    @Valid
    private List<MatchPlayerResultRequest> players;
}
