export default function FocusedLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-dvh flex-1 flex-col bg-background">
      {children}
    </div>
  );
}
