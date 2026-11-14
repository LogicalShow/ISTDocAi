export interface ApplicationModuleDto {
  Id: string;
  NameEn: string;
  NameAr: string;
  Modules: ModuleDto[];
}

export interface ModuleDto {
  Id: string;
  NameEn: string;
  NameAr: string;
  DirectPage: PageDto | null;
  Pages: PageDto[];
  Children: ModuleDto[];
}

export interface PageDto {
  Id: string;
  NameEn: string;
  NameAr: string;
  Actions: PageActionDto[];
  ChildPages: PageDto[];
}

export interface PageActionDto {
  Id: string;
  ActionId: string;
  ActionNameEn: string;
  ActionNameAr: string;
  PermissionKey: string;
  IsAssigned: boolean;
}

export interface LocalizedApplicationModuleDto extends Omit<ApplicationModuleDto, 'Modules'> {
  Name: string;
  Modules: LocalizedModuleDto[];
}

export interface LocalizedModuleDto extends Omit<ModuleDto, 'DirectPage' | 'Pages' | 'Children'> {
  Name: string;
  DirectPage: LocalizedPageDto | null;
  Pages: LocalizedPageDto[];
  Children: LocalizedModuleDto[];
}

export interface LocalizedPageDto extends Omit<PageDto, 'Actions' | 'ChildPages'> {
  Name: string;
  Actions: LocalizedPageActionDto[];
  ChildPages: LocalizedPageDto[];
}

export interface LocalizedPageActionDto extends PageActionDto {
  ActionName: string;
}
