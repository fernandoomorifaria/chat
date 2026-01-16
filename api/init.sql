CREATE TABLE users (
    id SERIAL PRIMARY KEY,
    nickname VARCHAR(255) NOT NULL UNIQUE,
    external_id VARCHAR(255) NOT NULL UNIQUE
);

CREATE TABLE rooms (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL UNIQUE
);

CREATE TABLE room_members (
    room_id INTEGER NOT NULL,
    user_id INTEGER NOT NULL,
    UNIQUE (room_id, user_id),
    FOREIGN KEY (room_id) REFERENCES rooms(id) ON DELETE CASCADE,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

INSERT INTO rooms (name) VALUES
('general');
