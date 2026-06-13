package com.lastround.backend.dto.match;

import jakarta.validation.Valid;
import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Size;
import lombok.Getter;
import lombok.Setter;

import java.util.List;

// Request DTO representing one player's in-game statistics within a MatchResultRequest.
@Getter
@Setter
public class MatchPlayerResultRequest {

    @NotNull
    private Long userId;

    // Accepted values are "WIN" or "LOSE" (validated by regex).
    @NotBlank
    @Size(max = 20)
    @Pattern(regexp = "WIN|LOSE")
    private String result;

    @NotNull
    @Min(0)
    @Max(10)
    private Integer score;

    @NotNull
    @Min(0)
    private Integer damageDealt;

    @Size(max = 80)
    private String characterName;

    // List of augments chosen by this player; validated recursively.
    @NotNull
    @Valid
    private List<MatchPlayerAugmentRequest> augments;
}