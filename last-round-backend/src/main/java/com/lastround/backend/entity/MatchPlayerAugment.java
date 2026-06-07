package com.lastround.backend.entity;

import jakarta.persistence.*;
import lombok.*;

import java.time.LocalDateTime;

@Getter
@Setter
@Builder
@NoArgsConstructor
@AllArgsConstructor
@Entity
@Table(name = "match_player_augments", uniqueConstraints = {
        @UniqueConstraint(name = "uk_match_user_round_order", columnNames = {
                "match_id", "user_id", "selected_round", "selected_order"
        })
}, indexes = {
        @Index(name = "idx_match_player_augments_match_id", columnList = "match_id"),
        @Index(name = "idx_match_player_augments_user_id", columnList = "user_id"),
        @Index(name = "idx_match_player_augments_augment_id", columnList = "augment_id")
})
public class MatchPlayerAugment {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "match_id", nullable = false)
    private Match match;

    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "user_id", nullable = false)
    private User user;

    // Unity augment 세트와 DB augments 시드가 다르므로 augment_id(FK)는 비워둘 수 있다.
    @ManyToOne(fetch = FetchType.LAZY, optional = true)
    @JoinColumn(name = "augment_id", nullable = true)
    private Augment augment;

    // Unity 클라이언트가 보낸 augment 이름(displayName)을 그대로 보존한다.
    @Column(name = "augment_name", length = 80)
    private String augmentName;

    @Column(name = "selected_order")
    private Integer selectedOrder;

    @Column(name = "selected_round")
    private Integer selectedRound;

    @Column(name = "created_at", nullable = false, updatable = false)
    private LocalDateTime createdAt;

    @PrePersist
    protected void onCreate() {
        if (this.createdAt == null) {
            this.createdAt = LocalDateTime.now();
        }
    }
}