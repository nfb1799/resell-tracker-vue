import { describe, it, expect } from 'vitest'
import { MAX_UPLOAD_BYTES, processPhoto } from '../image'

// The resizing itself needs a real canvas and is checked in the browser; these are
// the guards that run before any decoding starts.
describe('processPhoto', () => {
  it('refuses something that is not an image', async () => {
    await expect(processPhoto(new Blob(['%PDF'], { type: 'application/pdf' }))).rejects.toThrow('not an image')
  })

  it('refuses an image over 25MB before trying to decode it', async () => {
    const huge = { type: 'image/jpeg', size: MAX_UPLOAD_BYTES + 1 } as Blob
    await expect(processPhoto(huge)).rejects.toThrow('under 25MB')
  })
})
