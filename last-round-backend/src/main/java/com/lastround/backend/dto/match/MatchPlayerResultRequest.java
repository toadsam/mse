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

@Getter
@Setter
public class MatchPlayerResultRequest {

    @NotNull
    private Long userId;

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

    @NotNull
    @Valid
    private List<MatchPlayerAugmentRequest> augments;
}