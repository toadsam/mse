package com.lastround.backend.dto.match;

import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;
import lombok.Getter;
import lombok.Setter;

@Getter
@Setter
public class MatchPlayerAugmentRequest {

    // augmentId는 선택값: Unity augment가 DB augments 테이블에 존재할 때만 채워진다.
    private Long augmentId;

    // 저장 대상은 augment 이름. Unity의 displayName을 그대로 받는다.
    @Size(max = 80)
    private String augmentName;

    @NotNull
    @Min(1)
    private Integer selectedOrder;

    @NotNull
    @Min(1)
    private Integer selectedRound;
}