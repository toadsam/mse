package com.lastround.backend.dto.match;

import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotNull;
import lombok.Getter;
import lombok.Setter;

@Getter
@Setter
public class MatchPlayerAugmentRequest {

    @NotNull
    private Long augmentId;

    @NotNull
    @Min(1)
    private Integer selectedOrder;

    @NotNull
    @Min(1)
    private Integer selectedRound;
}