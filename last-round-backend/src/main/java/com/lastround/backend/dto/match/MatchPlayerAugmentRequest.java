package com.lastround.backend.dto.match;

import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;
import lombok.Getter;
import lombok.Setter;

// Request DTO for one augment selection submitted by a player within a match result.
@Getter
@Setter
public class MatchPlayerAugmentRequest {

    // augmentId is optional: only populated when the Unity augment name matches a DB entry.
    private Long augmentId;

    // The augment display name sent directly from Unity; stored as-is for historical accuracy.
    @Size(max = 80)
    private String augmentName;

    @NotNull
    @Min(1)
    private Integer selectedOrder;

    @NotNull
    @Min(1)
    private Integer selectedRound;
}