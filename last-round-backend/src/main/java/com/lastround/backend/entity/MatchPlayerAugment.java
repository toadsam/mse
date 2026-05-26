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

    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "augment_id", nullable = false)
    private Augment augment;

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