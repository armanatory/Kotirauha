const MAX_DIM = 1600;
const QUALITY = 0.7;

// Downscale and re-encode a photo to a reasonable size before upload, so phone
// pictures (often several MB) don't bloat storage or slow the upload. Falls back
// to the original file if anything goes wrong or the result isn't smaller.
export async function compressImage(file: File): Promise<File> {
  if (!file.type.startsWith("image/")) return file;
  try {
    const source = await loadImage(file);
    const sw = (source as HTMLImageElement).naturalWidth || source.width;
    const sh = (source as HTMLImageElement).naturalHeight || source.height;
    if (!sw || !sh) return file;

    const scale = Math.min(1, MAX_DIM / Math.max(sw, sh));
    const w = Math.round(sw * scale);
    const h = Math.round(sh * scale);

    const canvas = document.createElement("canvas");
    canvas.width = w;
    canvas.height = h;
    const ctx = canvas.getContext("2d");
    if (!ctx) return file;
    ctx.drawImage(source as CanvasImageSource, 0, 0, w, h);
    if ("close" in source) (source as ImageBitmap).close();

    const blob = await new Promise<Blob | null>((resolve) =>
      canvas.toBlob(resolve, "image/jpeg", QUALITY),
    );
    if (!blob || blob.size >= file.size) return file;

    const name = file.name.replace(/\.[^.]+$/, "") + ".jpg";
    return new File([blob], name, { type: "image/jpeg", lastModified: Date.now() });
  } catch {
    return file;
  }
}

async function loadImage(file: File): Promise<ImageBitmap | HTMLImageElement> {
  if (typeof createImageBitmap === "function") {
    try {
      return await createImageBitmap(file);
    } catch {
      /* some formats (e.g. certain HEIC) can't be decoded; fall back */
    }
  }
  return await new Promise((resolve, reject) => {
    const img = new Image();
    const url = URL.createObjectURL(file);
    img.onload = () => {
      URL.revokeObjectURL(url);
      resolve(img);
    };
    img.onerror = (e) => {
      URL.revokeObjectURL(url);
      reject(e);
    };
    img.src = url;
  });
}
