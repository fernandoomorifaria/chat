"use client"

import { HubConnection, HubConnectionBuilder } from "@microsoft/signalr";
import { getSession } from "next-auth/react";
import { FormEvent, useEffect, useRef, useState } from "react"

interface ChatProps {
  roomId: number
}

interface Message {
  type: string;
  nickname: string;
  content: string;
  timestamp: Date;
}

export default function Chat({ roomId }: ChatProps) {
  const [message, setMessage] = useState<string>("");
  const [messages, setMessages] = useState<Message[]>([]);
  const connectionRef = useRef<HubConnection>(null);

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(process.env.NEXT_PUBLIC_SIGNALR_URL!, {
        accessTokenFactory: async () => {
          const session = await getSession();

          return session?.accessToken!;
        }
      })
      .build();

    connectionRef.current = connection;

    connection.on("ReceiveMessageHistory", (messages: Message[]) => {
      setMessages(messages);
    })

    connection.on("ReceiveMessage", (message: Message) => {
      setMessages((prev) => [...prev, message]);
    });

    // TODO: I don't know, this looks weird, like too much for a component
    connection
      .start()
      .then(() => joinRoom(roomId));

    return () => {
      leaveRoom(roomId).finally(() => connection.stop());
    }
  }, [roomId]);

  const joinRoom = async (roomId: number) => {
    await connectionRef.current?.invoke("JoinRoom", roomId);
  }

  // TODO: Redirect to rooms page. Need to check how this is going to work on unmount
  const leaveRoom = async (roomId: number) => {
    await connectionRef.current?.invoke("LeaveRoom", roomId);
  }

  const sendMessage = async (roomId: number, content: string) => {
    await connectionRef.current?.invoke("SendMessage", roomId, content);
  }

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();

    const content = message.trimEnd();

    if (!content) {
      return;
    }

    await sendMessage(roomId, content);
  }

  return (
    <div>
      <div>
        {messages.map((message, index) => (
          <div key={index}>
            {message.type === 'system' ? (
              <div>
                <p>
                  {message.content}
                </p>
              </div>
            )
              : (
                <div>
                  <div>
                    <span>
                      {message.nickname}
                    </span>
                    <span>
                      {message.timestamp.toString()}
                    </span>
                  </div>
                  <p>
                    {message.content}
                  </p>
                </div>
              )}
          </div>
        ))}
      </div>
      <form onSubmit={handleSubmit}>
        <input type="text" value={message} onChange={event => setMessage(event.target.value)} />
        <button type="submit">
          Send
        </button>
      </form>
    </div>
  )
}
