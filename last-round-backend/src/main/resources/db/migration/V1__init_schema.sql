CREATE TABLE users (
    id BIGINT NOT NULL AUTO_INCREMENT,
    email VARCHAR(100) NOT NULL,
    password VARCHAR(255) NOT NULL,
    nickname VARCHAR(30) NOT NULL,
    created_at DATETIME(6) NOT NULL,
    CONSTRAINT pk_users PRIMARY KEY (id),
    CONSTRAINT uk_users_email UNIQUE (email),
    CONSTRAINT uk_users_nickname UNIQUE (nickname)
);

CREATE TABLE matches (
    id BIGINT NOT NULL AUTO_INCREMENT,
    player1id BIGINT NOT NULL,
    player2id BIGINT NOT NULL,
    winner_id BIGINT NOT NULL,
    player1score INT NOT NULL,
    player2score INT NOT NULL,
    created_at DATETIME(6) NOT NULL,
    CONSTRAINT pk_matches PRIMARY KEY (id)
);

CREATE INDEX idx_matches_created_at ON matches (created_at);
CREATE INDEX idx_matches_player1 ON matches (player1id);
CREATE INDEX idx_matches_player2 ON matches (player2id);
CREATE INDEX idx_matches_winner ON matches (winner_id);

CREATE TABLE augments (
    id BIGINT NOT NULL AUTO_INCREMENT,
    name VARCHAR(80) NOT NULL,
    description VARCHAR(500) NOT NULL,
    effect_type VARCHAR(60) NOT NULL,
    CONSTRAINT pk_augments PRIMARY KEY (id)
);

CREATE TABLE refresh_tokens (
    id BIGINT NOT NULL AUTO_INCREMENT,
    user_id BIGINT NOT NULL,
    token VARCHAR(512) NOT NULL,
    expires_at DATETIME(6) NOT NULL,
    created_at DATETIME(6) NOT NULL,
    CONSTRAINT pk_refresh_tokens PRIMARY KEY (id),
    CONSTRAINT idx_refresh_token UNIQUE (token),
    CONSTRAINT fk_refresh_tokens_user FOREIGN KEY (user_id) REFERENCES users (id)
);

CREATE INDEX idx_refresh_tokens_user_id ON refresh_tokens (user_id);