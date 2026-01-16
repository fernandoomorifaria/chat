import Chat from "./chat";

export default async function RoomPage({
  params
}: {
  params: Promise<{ roomId: string }>
}) {
  const { roomId } = await params;

  return (
    <div>
      <Chat roomId={parseInt(roomId)} />
    </div>
  )
}
