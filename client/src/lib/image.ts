// Photos are resized in the browser before they are uploaded. Two JPEGs come out
// of every pick:
//
//   thumbnail  ~96px, a few KB: rides along on every item in the list
//   full       ~900px, under 100KB: fetched only when that one item is opened
//
// The server checks both are JPEGs and within its limits (8 KB and 200 KB).

const THUMB_MAX_PX = 96
const FULL_MAX_PX = 900
const THUMB_QUALITY = 0.6
const FULL_QUALITY = 0.72

/** Rejecting oversized input up front beats decoding a 50MP photo on a phone. */
export const MAX_UPLOAD_BYTES = 25 * 1024 * 1024

export interface ProcessedPhoto {
  thumbnail: Blob
  full: Blob
}

function drawScaled(bitmap: ImageBitmap, maxPx: number, quality: number): Promise<Blob> {
  const scale = Math.min(1, maxPx / Math.max(bitmap.width, bitmap.height))
  const canvas = document.createElement('canvas')
  canvas.width = Math.max(1, Math.round(bitmap.width * scale))
  canvas.height = Math.max(1, Math.round(bitmap.height * scale))

  const ctx = canvas.getContext('2d')
  if (!ctx) throw new Error('This browser cannot resize images.')
  ctx.imageSmoothingQuality = 'high'
  // JPEG has no alpha; without this, transparent PNGs come out with black edges.
  ctx.fillStyle = '#ffffff'
  ctx.fillRect(0, 0, canvas.width, canvas.height)
  ctx.drawImage(bitmap, 0, 0, canvas.width, canvas.height)

  return new Promise((resolve, reject) =>
    canvas.toBlob((blob) => (blob ? resolve(blob) : reject(new Error('Could not encode the image.'))), 'image/jpeg', quality),
  )
}

export async function processPhoto(file: Blob): Promise<ProcessedPhoto> {
  if (!file.type.startsWith('image/')) throw new Error('That file is not an image.')
  if (file.size > MAX_UPLOAD_BYTES) throw new Error('That image is too large — pick one under 25MB.')

  // 'from-image' applies the EXIF rotation, so phone photos are not sideways.
  const bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' })
  try {
    const [thumbnail, full] = await Promise.all([
      drawScaled(bitmap, THUMB_MAX_PX, THUMB_QUALITY),
      drawScaled(bitmap, FULL_MAX_PX, FULL_QUALITY),
    ])
    return { thumbnail, full }
  } finally {
    bitmap.close()
  }
}
