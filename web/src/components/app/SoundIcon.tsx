/** Splits the catalog's concatenated lucide path data into <path> elements. */
interface SoundIconProps {
  path: string
  size?: number
  strokeWidth?: number
  className?: string
}

export function SoundIcon({
  path,
  size = 20,
  strokeWidth = 2,
  className,
}: SoundIconProps) {
  const segments = path.split(" M ")

  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={strokeWidth}
      strokeLinecap="round"
      strokeLinejoin="round"
      width={size}
      height={size}
      className={className}
      aria-hidden="true"
    >
      {segments.map((seg, i) => (
        <path key={i} d={i === 0 ? seg : `M ${seg}`} />
      ))}
    </svg>
  )
}
