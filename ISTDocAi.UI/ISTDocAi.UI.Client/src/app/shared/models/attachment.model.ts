export interface AttachmentAddRequest {
  originalFileName: string;
  note?: string;
  referenceType: string;
  referenceId: string;
  referenceSerial: string;
  file: File;
}

export interface AttachmentUpdateRequest {
  id: string;
  rowVersion: string;
  originalFileName: string;
  note?: string;
  referenceType: string;
  referenceId: string;
  referenceSerial: string;
  file?: File; // optional: if null, only metadata update
}

export interface AttachmentDeleteRequest {
  items: DeleteItem[];
}

export interface DeleteItem {
  id: string;
  rowVersion: string;
}

export interface AttachmentResponse {
  Id: string;
  RowVersion: string;
  StoredFileName: string;
  OriginalFileName: string;
  FilePath: string;
  ContentType?: string;
  FileExtension?: string;
  FileHash?: string;
  FileSizeInBytes: number;
  Note?: string;
  ReferenceType: string;
  ReferenceId: string;
  ReferenceSerial: string;
}

export interface AttachmentAddResponse {
  Id: string;
}

export interface AttachmentUpdateResponse {
  Message: string;
}

export interface BlockedDeleteItem {
  id: string;
  rowVersion: string;
  reason: string;
}

export interface AttachmentDeleteResponse {
  message: string;
  deletedIds: string[];
  blocked: BlockedDeleteItem[];
}
